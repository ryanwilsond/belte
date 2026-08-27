using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis;

internal sealed partial class TemplateMetadataReader {
    internal sealed partial class TemplateMetadata {
        private readonly Compilation _compilation;
        private readonly byte[] _bytes;
        private readonly MemoryStream _stream;
        private readonly BinaryReader _reader;

        private bool _isMalformed;

        private bool _headerAndTableSizesIsRead;

        private ushort _majorVersion;
        private ushort _minorVersion;
        private uint _headerSize;

        private bool _assemblyTableIsRead;

        private uint _assemblyTableSize;
        private uint _assemblyTableCount;
        private AssemblySymbol[] _assemblyTable;

        private bool _typeTableIsRead;

        private uint _typeTableSize;
        private uint _typeTableCount;
        private TypeEntry[] _typeTable;
        private NamedTypeSymbol[] _resolvedTypeTable;

        private bool _methodTableIsRead;

        private uint _methodTableSize;
        private uint _methodTableCount;
        private TemplateMethodDecoder[] _methodTable;
        private MethodSymbol[] _resolvedMethodTable;

        private bool _typeDefTableIsRead;

        private uint _typeDefTableSize;
        private uint _typeDefTableCount;
        private TemplateTypeDecoder[] _typeDefTable;

        private bool _templateTableIsRead;

        private uint _templateTableSize;
        private uint _templateTableCount;
        private Dictionary<uint, uint[]> _templateEntries;

        private bool _boundTableIsRead;

        private uint _boundTableSize;
        private uint _boundTableCount;
        private (uint offset, uint size, uint methodIndex)[] _boundEntries;

        private readonly Dictionary<TypeSymbol, long> _hasMetadataForTypeCache = [];
        private readonly Dictionary<TypeSymbol, ImmutableDictionary<MethodSymbol, BoundBlockStatement>> _decodedMethodsAndBodiesCache = [];

        internal TemplateMetadata(Compilation compilation, byte[] bytes) {
            _compilation = compilation;
            _bytes = bytes;
            _stream = new MemoryStream(_bytes);
            _reader = new BinaryReader(_stream);
        }

        internal void CreateTemplateTypes(ArrayBuilder<NamedTypeSymbol> builder, PENamespaceSymbol ns) {
            if (!_headerAndTableSizesIsRead)
                ReadHeader();

            if (_isMalformed)
                return;

            if (!_assemblyTableIsRead) {
                ReadAssemblyTable();

                if (_isMalformed)
                    return;
            }

            if (!_typeTableIsRead) {
                ReadTypeTable();

                if (_isMalformed)
                    return;
            }

            if (!_methodTableIsRead) {
                ReadMethodTable();

                if (_isMalformed)
                    return;
            }

            if (!_typeDefTableIsRead) {
                ReadTypeDefTable();

                if (_isMalformed)
                    return;
            }

            foreach (var decoder in _typeDefTable) {
                if (decoder.typeEntry.namespaceName == ns.metadataName && !decoder.isForSpecializationOnly)
                    builder.Add(new PETemplateType(ns, decoder));
            }
        }

        [Conditional("DEBUG")]
        internal void ForceComplete() {
            if (!_headerAndTableSizesIsRead)
                ReadHeader();

            if (_isMalformed)
                return;

            if (!_assemblyTableIsRead) {
                ReadAssemblyTable();

                if (_isMalformed)
                    return;
            }

            if (!_typeTableIsRead) {
                ReadTypeTable();

                if (_isMalformed)
                    return;
            }

            if (!_methodTableIsRead) {
                ReadMethodTable();

                if (_isMalformed)
                    return;
            }

            if (!_typeDefTableIsRead) {
                ReadTypeDefTable();

                if (_isMalformed)
                    return;
            }

            if (!_templateTableIsRead) {
                ReadTemplateTable();

                if (_isMalformed)
                    return;
            }

            if (_resolvedTypeTable is null)
                ResolveTypeTable();

            if (!_boundTableIsRead)
                ReadBoundTable();

            if (!_compilation.options.excludeReadingTemplateMetadata) {
                var dummyContainer = _compilation.GetBoundReferenceManager().referencedAssemblies[0].globalNamespace;

                foreach (var decoder in _typeDefTable) {
                    var type = new PETemplateType(dummyContainer, decoder);
                    var members = type.GetMembers();

                    foreach (var member in members) {
                        if (member is PETemplateType.MetadataMethodSymbol method) {
                            var body = method.TryDecodeMethodBody();
                            Debug.Assert(body is not null);
                        }
                    }
                }
            }
        }

        internal bool HasTemplateEntryForType(TypeSymbol type) {
            if (_hasMetadataForTypeCache.TryGetValue(type, out var cached))
                return cached != -1;

            if (!_headerAndTableSizesIsRead)
                ReadHeader();

            if (_isMalformed || _templateTableCount == 0 || _typeTableCount == 0 || _assemblyTableCount == 0)
                return CacheResult(-1);

            if (!_assemblyTableIsRead) {
                ReadAssemblyTable();

                if (_isMalformed)
                    return CacheResult(-1);
            }

            if (!_typeTableIsRead) {
                ReadTypeTable();

                if (_isMalformed)
                    return CacheResult(-1);
            }

            if (!_templateTableIsRead) {
                ReadTemplateTable();

                if (_isMalformed)
                    return CacheResult(-1);
            }

            // We don't use this right now but we want to make sure it's not malformed
            if (!_typeDefTableIsRead) {
                if (!_methodTableIsRead) {
                    ReadMethodTable();

                    if (_isMalformed)
                        return CacheResult(-1);
                }

                ReadTypeDefTable();

                if (_isMalformed)
                    return CacheResult(-1);
            }

            var foundTypeEntryIndex = -1;

            for (var i = 0; i < _typeTableCount; i++) {
                var typeEntry = _typeTable[i];

                if (typeEntry.name != type.metadataName ||
                    typeEntry.arity != type.GetArity() ||
                    typeEntry.namespaceName != type.containingNamespace.metadataName) {
                    continue;
                }

                if (Array.IndexOf(_assemblyTable, type.containingAssembly) == typeEntry.assemblyIndex) {
                    foundTypeEntryIndex = i;
                    break;
                }
            }

            if (foundTypeEntryIndex == -1)
                return CacheResult(-1);

            Debug.Assert(foundTypeEntryIndex >= 0);

            if (_templateEntries.ContainsKey((uint)foundTypeEntryIndex))
                return CacheResult(foundTypeEntryIndex);

            return CacheResult(-1);

            bool CacheResult(long result) {
                _hasMetadataForTypeCache.Add(type, result);
                return result != -1;
            }
        }

        internal ImmutableDictionary<MethodSymbol, BoundBlockStatement> DecodeMethodsAndBodiesForType(TypeSymbol type) {
            if (_decodedMethodsAndBodiesCache.TryGetValue(type, out var found))
                return found;

            Debug.Assert(_headerAndTableSizesIsRead);
            Debug.Assert(_assemblyTableIsRead);
            Debug.Assert(_typeTableIsRead);
            Debug.Assert(_templateTableIsRead);
            Debug.Assert(_typeDefTableIsRead);
            Debug.Assert(_hasMetadataForTypeCache.ContainsKey(type) && _hasMetadataForTypeCache[type] != -1);

            var typeEntryIndex = _hasMetadataForTypeCache[type];

            PETemplateType templateType = null;

            foreach (var decoder in _typeDefTable) {
                if (decoder.typeEntryIndex == typeEntryIndex) {
#if DEBUG
                    Debug.Assert(templateType is null);
#endif
                    templateType = new PETemplateType(type.containingNamespace, decoder, (NamedTypeSymbol)type);
#if !DEBUG
                    break;
#endif
                }
            }

            var builder = ImmutableDictionary.CreateBuilder<MethodSymbol, BoundBlockStatement>();
            var members = templateType.GetMembers();

            foreach (var member in members) {
                if (member.kind == SymbolKind.Method) {
                    var metadataMethod = (PETemplateType.MetadataMethodSymbol)member;
                    var body = metadataMethod.TryDecodeMethodBody();
                    Debug.Assert(body is not null);
                    builder.Add(metadataMethod, body);
                }
            }

            var dictionary = builder.ToImmutableDictionary();
            _decodedMethodsAndBodiesCache.Add(type, dictionary);
            return dictionary;
        }

        private void ReadHeader() {
            Debug.Assert(!_headerAndTableSizesIsRead);

            lock (this) {
                _reader.BaseStream.Seek(0, SeekOrigin.Begin);
                var magic = Encoding.UTF8.GetString(_reader.ReadBytes(4));

                if (magic != "BLTM") {
                    _isMalformed = true;
                    _headerAndTableSizesIsRead = true;
                    return;
                }

                _majorVersion = _reader.ReadUInt16();
                _minorVersion = _reader.ReadUInt16();

                if (_majorVersion != TemplateMetadataWriter.MajorVersion) {
                    _isMalformed = true;
                    _headerAndTableSizesIsRead = true;
                    return;
                }

                _headerSize = _reader.ReadUInt32();

                if (_headerSize > _reader.BaseStream.Length + 48) {
                    _isMalformed = true;
                    _headerAndTableSizesIsRead = true;
                    return;
                }

                _reader.BaseStream.Seek(_headerSize, SeekOrigin.Begin);

                _assemblyTableSize = _reader.ReadUInt32();
                _assemblyTableCount = _reader.ReadUInt32();

                if (_headerSize + _assemblyTableSize > _reader.BaseStream.Length + 40) {
                    _isMalformed = true;
                    _headerAndTableSizesIsRead = true;
                    return;
                }

                _reader.BaseStream.Seek(_headerSize + _assemblyTableSize, SeekOrigin.Begin);

                _typeTableSize = _reader.ReadUInt32();
                _typeTableCount = _reader.ReadUInt32();

                if (_headerSize + _assemblyTableSize + _typeTableSize > _reader.BaseStream.Length + 32) {
                    _isMalformed = true;
                    _headerAndTableSizesIsRead = true;
                    return;
                }

                _reader.BaseStream.Seek(_headerSize + _assemblyTableSize + _typeTableSize, SeekOrigin.Begin);

                _methodTableSize = _reader.ReadUInt32();
                _methodTableCount = _reader.ReadUInt32();

                if (_headerSize + _assemblyTableSize + _typeTableSize + _methodTableSize
                        > _reader.BaseStream.Length + 24) {
                    _isMalformed = true;
                    _headerAndTableSizesIsRead = true;
                    return;
                }

                _reader.BaseStream.Seek(
                    _headerSize + _assemblyTableSize + _typeTableSize + _methodTableSize,
                    SeekOrigin.Begin
                );

                _typeDefTableSize = _reader.ReadUInt32();
                _typeDefTableCount = _reader.ReadUInt32();

                if (_headerSize + _assemblyTableSize + _typeTableSize + _methodTableSize + _typeDefTableSize
                        > _reader.BaseStream.Length + 16) {
                    _isMalformed = true;
                    _headerAndTableSizesIsRead = true;
                    return;
                }

                _reader.BaseStream.Seek(
                    _headerSize + _assemblyTableSize + _typeTableSize + _methodTableSize + _typeDefTableSize,
                    SeekOrigin.Begin
                );

                _templateTableSize = _reader.ReadUInt32();
                _templateTableCount = _reader.ReadUInt32();

                if (_headerSize + _assemblyTableSize + _typeTableSize +
                        _methodTableSize + _typeDefTableSize + _templateTableSize
                        > _reader.BaseStream.Length + 8) {
                    _isMalformed = true;
                    _headerAndTableSizesIsRead = true;
                    return;
                }

                _reader.BaseStream.Seek(
                    _headerSize + _assemblyTableSize + _typeTableSize +
                        _methodTableSize + _typeDefTableSize + _templateTableSize,
                    SeekOrigin.Begin
                );

                _boundTableSize = _reader.ReadUInt32();
                _boundTableCount = _reader.ReadUInt32();

                var totalSize = _headerSize + _assemblyTableSize + _typeTableSize +
                    _methodTableSize + _typeDefTableSize + _templateTableSize + _boundTableSize;

                if (totalSize != _reader.BaseStream.Length)
                    _isMalformed = true;

                _headerAndTableSizesIsRead = true;
            }
        }

        private void ReadAssemblyTable() {
            Debug.Assert(!_assemblyTableIsRead);

            lock (this) {
                _reader.BaseStream.Seek(_headerSize + 8, SeekOrigin.Begin);
                _assemblyTable = new AssemblySymbol[_assemblyTableCount];

                for (var i = 0; i < _assemblyTableCount; i++) {
                    var startPosition = _reader.BaseStream.Position;
                    var entrySize = _reader.ReadUInt32();

                    if (startPosition + entrySize > _reader.BaseStream.Length) {
                        _isMalformed = true;
                        _assemblyTableIsRead = true;
                        return;
                    }

                    var identitySize = entrySize - 4;
                    var identityDisplay = Encoding.UTF8.GetString(_reader.ReadBytes((int)identitySize));

                    AssemblySymbol symbol = null;

                    var referencedAssemblies = _compilation.GetBoundReferenceManager().referencedAssemblies;

                    foreach (var referencedAssembly in referencedAssemblies) {
                        if (string.Equals(
                            referencedAssembly.identity.GetDisplayName(fullKey: true),
                            identityDisplay,
                            System.StringComparison.Ordinal)) {
                            Debug.Assert(symbol is null);
                            symbol = referencedAssembly;
#if !DEBUG
                            break;
#endif
                        }
                    }

                    if (symbol is null) {
                        if (MetadataHelpers.IsCorLibraryName(identityDisplay.Split(',')[0])) {
                            symbol = _compilation.assembly.corAssembly;
                        } else {
                            _isMalformed = true;
                            continue;
                        }
                    }

                    _assemblyTable[i] = symbol;
                }

                _assemblyTableIsRead = true;
            }

            Debug.Assert(_assemblyTableIsRead);
        }

        private void ReadTypeTable() {
            Debug.Assert(_assemblyTableIsRead);
            Debug.Assert(!_typeTableIsRead);

            lock (this) {
                _reader.BaseStream.Seek(_headerSize + _assemblyTableSize + 8, SeekOrigin.Begin);
                _typeTable = new TypeEntry[_typeTableCount];

                for (var i = 0; i < _typeTableCount; i++) {
                    var startPosition = _reader.BaseStream.Position;
                    var entrySize = _reader.ReadUInt32();

                    if (startPosition + entrySize > _reader.BaseStream.Length) {
                        _isMalformed = true;
                        _typeTableIsRead = true;
                        return;
                    }

                    var nameSize = _reader.ReadUInt32();
                    var name = Encoding.UTF8.GetString(_reader.ReadBytes((int)nameSize));
                    var arity = _reader.ReadUInt16();
                    var flags = (TemplateMetadataWriter.TypeFlags)_reader.ReadByte();
                    var namespaceNameSize = _reader.ReadUInt32();
                    var namespaceName = Encoding.UTF8.GetString(_reader.ReadBytes((int)namespaceNameSize));
                    var assemblyIndex = _reader.ReadUInt32();
                    var isNested = _reader.ReadBoolean();
                    var containingTypeIndex = _reader.ReadUInt32();

                    if (_reader.BaseStream.Position != startPosition + entrySize) {
                        _isMalformed = true;
                        _typeTableIsRead = true;
                        return;
                    }

                    if (assemblyIndex >= _assemblyTable.Length &&
                        (flags & (TemplateMetadataWriter.TypeFlags.IsNullable |
                                  TemplateMetadataWriter.TypeFlags.IsInMemoryLibraryType)) == 0) {
                        _isMalformed = true;
                        continue;
                    }

                    _typeTable[i] = new TypeEntry(
                        name,
                        arity,
                        flags,
                        namespaceName,
                        assemblyIndex,
                        isNested,
                        containingTypeIndex
                    );
                }

                _typeTableIsRead = true;
            }

            Debug.Assert(_typeTableIsRead);
        }

        private TypeSymbol ResolveType(uint index) {
            if (index >= _typeTableCount)
                return null;

            _resolvedTypeTable ??= new NamedTypeSymbol[_typeTableCount];

            if (_resolvedTypeTable[index] is not null)
                return _resolvedTypeTable[index];

            var entry = _typeTable[index];

            if (entry.flags != 0) {
                if ((entry.flags & TemplateMetadataWriter.TypeFlags.IsObject) != 0) {
                    var objectType = _compilation.GetSpecialType(SpecialType.Object);
                    _resolvedTypeTable[index] = objectType;
                    return objectType;
                }

                if ((entry.flags & TemplateMetadataWriter.TypeFlags.IsNullable) != 0) {
                    var nullableType = _compilation.GetSpecialType(SpecialType.Nullable);
                    _resolvedTypeTable[index] = nullableType;
                    return nullableType;
                }

                if ((entry.flags & TemplateMetadataWriter.TypeFlags.IsInMemoryLibraryType) != 0) {
                    var symbol = _compilation.corLibrary.belteNamespace.GetTypeMembers(entry.name).Single();
                    _resolvedTypeTable[index] = symbol;
                    return symbol;
                }
            }

            if (entry.isNested) {
                var containingType = ResolveType(entry.containingTypeIndex);
                var nestedTypes = containingType.GetTypeMembers(entry.name);
                NamedTypeSymbol foundType = null;

                foreach (var candidate in nestedTypes) {
                    if (candidate.arity == entry.arity) {
#if DEBUG
                        Debug.Assert(foundType is null);
#endif
                        foundType = candidate;
#if !DEBUG
                        break;
#endif
                    }
                }

                Debug.Assert(foundType is not null && !foundType.IsErrorType());
                _resolvedTypeTable[index] = foundType;
                return foundType;
            }

            var assembly = _assemblyTable[entry.assemblyIndex];

            var metadataName = MetadataTypeName.FromNamespaceAndTypeName(
                entry.namespaceName,
                entry.name,
                forcedArity: entry.arity
            );

            var type = assembly.LookupDeclaredTopLevelMetadataType(ref metadataName);
            _resolvedTypeTable[index] = type;

            Debug.Assert(type is not null && !type.IsErrorType());

            return type;
        }

        internal MethodSymbol ResolveMethod(uint index) {
            if (index >= _methodTableCount) {
                Debug.Assert(false);
                return null;
            }

            _resolvedMethodTable ??= new MethodSymbol[_methodTableCount];

            if (_resolvedMethodTable[index] is not null)
                return _resolvedMethodTable[index];

            var methodDecoder = _methodTable[index];
            var methodEntry = methodDecoder.methodEntry;
            var typeSymbol = ResolveType(methodEntry.containingTypeIndex);

            if (methodDecoder.enclosingContext is null)
                methodDecoder.SetEnclosingContext(typeSymbol);

            if (typeSymbol is null) {
                Debug.Assert(false);
                return null;
            }

            var flags = methodDecoder.GetAdditionalFlags();

            if ((flags & TemplateMetadataWriter.MethodFlags.IsWellKnownMember) != 0) {
                var wellKnownMember = (WellKnownMember)((ushort)flags & 0xFF);
                var member = _compilation.corLibrary.GetWellKnownMethod(wellKnownMember)
                    .AsMember((NamedTypeSymbol)typeSymbol);

                _resolvedMethodTable[index] = member;
                return member;
            }

            var candidates = typeSymbol.GetMembers(methodEntry.name);

            if (candidates.Length == 0) {
                Debug.Assert(false);
                return null;
            }

            if (candidates.Length == 1) {
                var method = candidates[0] as MethodSymbol;
                _resolvedMethodTable[index] = method;
                return method;
            }

            var filteredCount = candidates.Count(c => c is MethodSymbol m && m.arity == methodEntry.arity);

            if (filteredCount == 0) {
                Debug.Assert(false);
                return null;
            }

            if (filteredCount == 1) {
                var method = (MethodSymbol)candidates.Single(c => c is MethodSymbol m && m.arity == methodEntry.arity);
                _resolvedMethodTable[index] = method;
                return method;
            }

            foreach (var candidate in candidates) {
                if (candidate is not MethodSymbol m || m.arity != methodEntry.arity)
                    continue;

                if (m.parameterCount != methodDecoder.GetParameterCount())
                    continue;

                if (m.returnsByRef !=
                    ((methodDecoder.GetReturnFlags() & TemplateMetadataWriter.ReturnFlags.ByRef) != 0)) {
                    continue;
                }

                if (!m.returnType.Equals(methodDecoder.GetReturnType(), TypeCompareKind.ConsiderEverything))
                    continue;

                var sameSignature = true;

                for (var i = 0; i < m.parameterCount; i++) {
                    var param1 = m.parameters[i];
                    var param2 = methodDecoder.DecodeParameter((uint)i);

                    if (!param1.type.Equals(param2.Item3, TypeCompareKind.ConsiderEverything)) {
                        sameSignature = false;
                        break;
                    }
                }

                if (sameSignature) {
                    _resolvedMethodTable[index] = m;
                    return m;
                }
            }

            Debug.Assert(false);
            return null;
        }

        private void ResolveTypeTable() {
            for (uint i = 0; i < _typeTableCount; i++) {
                var type = ResolveType(i);
                Debug.Assert(type is not null && !type.IsErrorType());
            }
        }

        private void ReadMethodTable() {
            Debug.Assert(_typeTableIsRead);
            Debug.Assert(!_methodTableIsRead);

            lock (this) {
                _reader.BaseStream.Seek(
                    _headerSize + _assemblyTableSize + _typeTableSize + 8,
                    SeekOrigin.Begin
                );

                _methodTable = new TemplateMethodDecoder[_methodTableCount];

                for (uint i = 0; i < _methodTableCount; i++) {
                    var startPosition = _reader.BaseStream.Position;
                    var entrySize = _reader.ReadUInt32();

                    if (startPosition + entrySize > _reader.BaseStream.Length) {
                        _isMalformed = true;
                        _methodTableIsRead = true;
                        return;
                    }

                    var nameSize = _reader.ReadUInt32();
                    var name = Encoding.UTF8.GetString(_reader.ReadBytes((int)nameSize));

                    var offsetAfterName = (uint)_reader.BaseStream.Position;

                    var arity = _reader.ReadUInt16();
                    var containingTypeIndex = _reader.ReadUInt32();

                    if (containingTypeIndex >= _typeTableCount) {
                        _isMalformed = true;
                        continue;
                    }

                    var methodEntry = new MethodEntry(name, arity, containingTypeIndex, i);

                    _methodTable[i] = new TemplateMethodDecoder(
                        this,
                        (uint)startPosition,
                        offsetAfterName,
                        entrySize,
                        methodEntry
                    );

                    _reader.BaseStream.Seek(
                        entrySize - (_reader.BaseStream.Position - startPosition),
                        SeekOrigin.Current
                    );
                }

                _methodTableIsRead = true;
            }

            Debug.Assert(_methodTableIsRead);
        }

        private void ReadTypeDefTable() {
            Debug.Assert(_methodTableIsRead);
            Debug.Assert(_resolvedTypeTable is null);
            Debug.Assert(!_typeDefTableIsRead);

            lock (this) {
                _reader.BaseStream.Seek(
                    _headerSize + _assemblyTableSize + _typeTableSize + _methodTableSize + 8,
                    SeekOrigin.Begin
                );

                _typeDefTable = new TemplateTypeDecoder[_typeDefTableCount];

                for (var i = 0; i < _typeDefTableCount; i++) {
                    var startPosition = _reader.BaseStream.Position;
                    var entrySize = _reader.ReadUInt32();

                    if (startPosition + entrySize > _reader.BaseStream.Length) {
                        _isMalformed = true;
                        _typeDefTableIsRead = true;
                        return;
                    }

                    var typeEntryIndex = _reader.ReadUInt32();
                    var typeEntry = _typeTable[typeEntryIndex];

                    var flags = (TemplateMetadataWriter.TypeDefFlags)_reader.ReadByte();

                    _typeDefTable[i] = new TemplateTypeDecoder(
                        this,
                        (uint)startPosition,
                        entrySize,
                        typeEntry,
                        typeEntryIndex,
                        (flags & TemplateMetadataWriter.TypeDefFlags.IsForSpecializationOnly) != 0
                    );

                    _reader.BaseStream.Seek(entrySize - 9, SeekOrigin.Current);
                }

                _typeDefTableIsRead = true;
            }

            Debug.Assert(_typeDefTableIsRead);
        }

        private void ReadTemplateTable() {
            Debug.Assert(!_templateTableIsRead);

            lock (this) {
                _reader.BaseStream.Seek(
                    _headerSize + _assemblyTableSize + _typeTableSize + _methodTableSize + _typeDefTableSize + 8,
                    SeekOrigin.Begin
                );

                _templateEntries = new Dictionary<uint, uint[]>((int)_templateTableCount);

                for (var i = 0; i < _templateTableCount; i++) {
                    var startPosition = _reader.BaseStream.Position;
                    var entrySize = _reader.ReadUInt32();
                    _ = _reader.ReadUInt16(); // Flags unused currently
                    var typeIndex = _reader.ReadUInt32();
                    var boundEntryCount = _reader.ReadUInt32();

                    var boundEntryIndexes = new uint[boundEntryCount];

                    for (var j = 0; j < boundEntryCount; j++)
                        boundEntryIndexes[j] = _reader.ReadUInt32();

                    if (_reader.BaseStream.Position != startPosition + entrySize) {
                        _isMalformed = true;
                        _templateTableIsRead = true;
                        return;
                    }

                    _templateEntries.Add(typeIndex, boundEntryIndexes);
                }

                _templateTableIsRead = true;
            }

            Debug.Assert(_templateTableIsRead);
        }

        private void ReadBoundTable() {
            Debug.Assert(!_boundTableIsRead);

            lock (this) {
                _reader.BaseStream.Seek(
                    _headerSize + _assemblyTableSize + _typeTableSize + _methodTableSize +
                        _typeDefTableSize + _templateTableSize + 8,
                    SeekOrigin.Begin
                );

                _boundEntries = new (uint, uint, uint)[_boundTableCount];

                for (var i = 0; i < _boundTableCount; i++) {
                    var entrySize = _reader.ReadUInt32();
                    var methodIndex = _reader.ReadUInt32();

                    if (methodIndex >= _methodTableCount) {
                        _isMalformed = true;
                        _reader.BaseStream.Seek(entrySize - 8, SeekOrigin.Current);
                        continue;
                    }

                    _boundEntries[i] = ((uint)_reader.BaseStream.Position, entrySize, methodIndex);

                    _reader.BaseStream.Seek(entrySize - 8, SeekOrigin.Current);
                }

                _boundTableIsRead = true;
            }

            Debug.Assert(_boundTableIsRead);
        }

        internal bool TryGetBoundTableOffsetForMethod(MethodEntry entry, out uint offset, out uint boundIRSize) {
            if (!_templateTableIsRead)
                ReadTemplateTable();

            if (!_boundTableIsRead)
                ReadBoundTable();

            if (_isMalformed) {
                offset = 0;
                boundIRSize = 0;
                return false;
            }

            // We have 2 ways to find the bound entry
            //  1. Iterate through each bound entry to find a matching method index
            //  2. Find the method's enclosing type entry in the template table and use the listed bound entry indexes
            // For metadata verification we do both and check if they validate each other, otherwise treat the metadata
            // as malformed

            lock (this) {
                uint boundEntryIndex = 0;
                uint potentialOffset = 0;
                uint potentialSize = 0;

                for (uint i = 0; i < _boundTableCount; i++) {
                    var boundEntry = _boundEntries[i];

                    if (boundEntry.methodIndex == entry.methodIndex) {
                        if (potentialOffset != 0) {
                            _isMalformed = true;
                            offset = 0;
                            boundIRSize = 0;
                            return false;
                        }

                        potentialOffset = boundEntry.offset;
                        boundEntryIndex = i;
                        potentialSize = boundEntry.size - 8;
                    }
                }

                if (potentialOffset == 0) {
                    // The metadata is malformed per se, but the method entry is missing
                    offset = 0;
                    boundIRSize = 0;
                    return false;
                }

                if (!_templateEntries.TryGetValue(entry.containingTypeIndex, out var templateEntry)) {
                    offset = 0;
                    boundIRSize = 0;
                    return false;
                }

                if (!templateEntry.Contains(boundEntryIndex)) {
                    // Template table not in agreement with bound entry table
                    _isMalformed = true;
                    offset = 0;
                    boundIRSize = 0;
                    return false;
                }

                offset = potentialOffset;
                boundIRSize = potentialSize;
                return true;
            }
        }
    }
}
