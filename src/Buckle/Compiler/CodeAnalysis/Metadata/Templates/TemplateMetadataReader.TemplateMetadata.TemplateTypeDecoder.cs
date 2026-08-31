using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis;

internal sealed partial class TemplateMetadataReader {
    internal sealed partial class TemplateMetadata {
        internal sealed class TemplateTypeDecoder : TemplateDecoder {
            private readonly TypeEntry _typeEntry;
            private readonly bool _isForSpecializationOnly;
            private readonly uint _typeEntryIndex;

            private NamedTypeSymbol _baseType;
            private uint _offsetAfterBaseType;

            private bool _readTemplateParameters;
            private TemplateParameterInfo[] _templateParameters;
            private uint _offsetAfterTemplateParameters;

            private ImmutableArray<NamedTypeSymbol> _interfaces;
            private uint _offsetAfterInterfaces;

            private FieldInfo[] _fields;
            private uint _offsetAfterFields;

            private uint[] _methodIndexes;
            private uint _offsetAfterMethods;

            private bool _readConstraints;
            private BoundExpression[] _constraints;
            private uint _offsetAfterConstraints;

            private bool _readAttributes;
            private AttributeData[] _attributes;

            internal TemplateTypeDecoder(
                TemplateMetadata metadata,
                uint offset,
                uint size,
                TypeEntry typeEntry,
                uint typeEntryIndex,
                bool isForSpecializationOnly)
                : base(metadata, offset, size) {
                _typeEntry = typeEntry;
                _isForSpecializationOnly = isForSpecializationOnly;
                _typeEntryIndex = typeEntryIndex;
            }

            internal TypeEntry typeEntry => _typeEntry;

            internal bool isForSpecializationOnly => _isForSpecializationOnly;

            internal uint typeEntryIndex => _typeEntryIndex;

            internal string GetMetadataName() {
                return _typeEntry.name;
            }

            internal ushort GetArity() {
                return _typeEntry.arity;
            }

            internal TemplateParameterInfo DecodeTemplateParameter(uint ordinal) {
                if (ordinal >= _typeEntry.arity)
                    throw ExceptionUtilities.Unreachable();

                DecodeTemplateParameters();

                return _templateParameters[ordinal];
            }

            internal ImmutableArray<NamedTypeSymbol> DecodeInterfaces() {
                if (_interfaces != default)
                    return _interfaces;

                DecodeTemplateParameters();
                Debug.Assert(_offsetAfterTemplateParameters != 0);

                lock (_metadata) lock (this) lock (_reader) {
                    _reader.BaseStream.Seek(_offsetAfterTemplateParameters, SeekOrigin.Begin);

                    var count = _reader.ReadUInt16();
                    var builder = ArrayBuilder<NamedTypeSymbol>.GetInstance(count);

                    for (var i = 0; i < count; i++) {
                        var interfaceKind = _reader.ReadByte();
                        var interfaceSymbol = ReadTypeSymbol(interfaceKind, _reader) as NamedTypeSymbol;
                        builder.Add(interfaceSymbol);
                    }

                    _interfaces = builder.ToImmutableAndFree();
                    _offsetAfterInterfaces = (uint)_reader.BaseStream.Position;
                }

                return _interfaces;
            }

            internal ImmutableArray<string> GetFieldNames() {
                return DecodeFields().SelectAsArray(t => t.name);
            }

            internal ImmutableArray<string> GetMethodNames() {
                var indexes = DecodeMethodIndexes();
                var builder = ArrayBuilder<string>.GetInstance(indexes.Length);

                foreach (var index in indexes) {
                    var methodEntry = _metadata._methodTable[index].methodEntry;
                    builder.Add(methodEntry.name);
                }

                return builder.ToImmutableAndFree();
            }

            internal FieldInfo[] DecodeFields() {
                if (_fields is not null)
                    return _fields;

                _ = DecodeInterfaces();

                lock (_metadata) lock (this) lock (_reader) {
                    _reader.BaseStream.Seek(_offsetAfterInterfaces, SeekOrigin.Begin);

                    var count = _reader.ReadUInt16();

                    _fields = new FieldInfo[count];

                    for (var i = 0; i < count; i++) {
                        var nameSize = _reader.ReadUInt32();
                        var name = Encoding.UTF8.GetString(_reader.ReadBytes((int)nameSize));
                        var flags = (TemplateMetadataWriter.FieldFlags)_reader.ReadByte();
                        var attributes = (FieldAttributes)_reader.ReadUInt32();
                        var typeKind = _reader.ReadByte();
                        var type = ReadTypeSymbol(typeKind, _reader);
                        var customAttributes = DecodeCustomAttributesCore((uint)_reader.BaseStream.Position);

                        ConstantValue defaultValue = null;

                        if ((attributes & FieldAttributes.HasDefault) != 0) {
                            defaultValue = ReadTypeOrConstant(type, _reader).constant;
                            Debug.Assert(defaultValue is not null);
                        }

                        _fields[i] = new FieldInfo(name, attributes, flags, type, customAttributes, defaultValue);
                    }

                    _offsetAfterFields = (uint)_reader.BaseStream.Position;
                }

                return _fields;
            }

            internal uint[] DecodeMethodIndexes() {
                if (_methodIndexes is not null)
                    return _methodIndexes;

                _ = DecodeFields();

                lock (_metadata) lock (this) lock (_reader) {
                    _reader.BaseStream.Seek(_offsetAfterFields, SeekOrigin.Begin);

                    var count = _reader.ReadUInt16();

                    _methodIndexes = new uint[count];

                    for (var i = 0; i < count; i++)
                        _methodIndexes[i] = _reader.ReadUInt32();

                    _offsetAfterMethods = (uint)_reader.BaseStream.Position;
                }

                return _methodIndexes;
            }

            internal TemplateMethodDecoder GetMethodDecoder(uint index) {
                if (index >= _metadata._methodTableCount)
                    return null;

                return _metadata._methodTable[index];
            }

            internal BoundExpression[] DecodeConstraints() {
                if (_readConstraints)
                    return _constraints;

                _ = DecodeMethodIndexes();

                lock (_metadata) lock (this) lock (_reader) {
                    var position = _reader.BaseStream.Seek(_offsetAfterMethods, SeekOrigin.Begin);
                    Debug.Assert(_reader.BaseStream.Position == position && position == _offsetAfterMethods);

                    var count = _reader.ReadUInt16();
                    Debug.Assert(_reader.BaseStream.Position == _offsetAfterMethods + 2);

                    _constraints = new BoundExpression[count];

                    for (var i = 0; i < count; i++) {
                        var startPosition = (uint)_reader.BaseStream.Position;
                        var size = _reader.ReadUInt32();
                        var offset = startPosition + 4;
                        Debug.Assert(_reader.BaseStream.Position == offset);

                        _constraints[i] = BoundNodeDecoder.DecodeConstraint(
                            enclosingContext,
                            this,
                            _metadata,
                            _reader,
                            offset,
                            size - 4
                        );
                    }

                    _offsetAfterConstraints = (uint)_reader.BaseStream.Position;
                    _readConstraints = true;
                }

                return _constraints;
            }

            internal AttributeData[] DecodeCustomAttributes() {
                if (_readAttributes)
                    return _attributes;

                _ = DecodeConstraints();

                _attributes = DecodeCustomAttributesCore(_offsetAfterConstraints);
                _readAttributes = true;

                return _attributes;
            }

            private void DecodeTemplateParameters() {
                if (_readTemplateParameters)
                    return;

                _ = GetBaseType();

                lock (_metadata) lock (this) lock (_reader) {
                    var position = _reader.BaseStream.Seek(_offsetAfterBaseType, SeekOrigin.Begin);
                    Debug.Assert(_reader.BaseStream.Position == position && position == _offsetAfterBaseType);

                    var count = _reader.ReadUInt16();
                    Debug.Assert(_reader.BaseStream.Position == _offsetAfterBaseType + 2);
                    Debug.Assert(count == _typeEntry.arity);

                    _templateParameters = new TemplateParameterInfo[count];

                    for (var i = 0; i < count; i++) {
                        var startOfParam = _reader.BaseStream.Position;
                        var nameSize = _reader.ReadUInt32();
                        Debug.Assert(_reader.BaseStream.Position == startOfParam + 4);
                        var name = Encoding.UTF8.GetString(_reader.ReadBytes((int)nameSize));
                        var flags = (TemplateMetadataWriter.TemplateParameterFlags)_reader.ReadByte();
                        var attributes = (GenericParameterAttributes)_reader.ReadUInt32();
                        var underlyingKind = _reader.ReadByte();
                        var underlyingType = ReadTypeSymbol(underlyingKind, _reader);
                        TypeOrConstant defaultValue = null;

                        if ((flags & TemplateMetadataWriter.TemplateParameterFlags.HasDefaultValue) != 0)
                            defaultValue = ReadTypeOrConstant(underlyingType, _reader);

                        var customAttributes = DecodeCustomAttributesCore((uint)_reader.BaseStream.Position);
                        var constraintTypeCount = _reader.ReadUInt16();
                        var constraintTypes = new TypeSymbol[constraintTypeCount];

                        for (var j = 0; j < constraintTypeCount; j++) {
                            var kind = _reader.ReadByte();
                            constraintTypes[j] = ReadTypeSymbol(kind, _reader);
                        }

                        _templateParameters[i] = new TemplateParameterInfo(
                            name,
                            attributes,
                            flags,
                            underlyingType,
                            defaultValue,
                            customAttributes,
                            constraintTypes
                        );
                    }

                    _offsetAfterTemplateParameters = (uint)_reader.BaseStream.Position;
                    _readTemplateParameters = true;
                }
            }

            internal TypeAttributes GetTypeFlags() {
                lock (_metadata) lock (_reader) {
                    _reader.BaseStream.Seek(_offset + 9, SeekOrigin.Begin);
                    return (TypeAttributes)_reader.ReadUInt32();
                }
            }

            internal NamedTypeSymbol GetBaseType() {
                if (_baseType is null) {
                    lock (_metadata) lock (this) lock (_reader) {
                        _reader.BaseStream.Seek(_offset + 13, SeekOrigin.Begin);
                        var kind = _reader.ReadByte();
                        Debug.Assert(_reader.BaseStream.Position == _offset + 14);
                        _baseType = ReadTypeSymbol(kind, _reader) as NamedTypeSymbol;
                        _offsetAfterBaseType = (uint)_reader.BaseStream.Position;
                    }
                }

                return _baseType;
            }
        }
    }
}
