using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Threading;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract partial class PENamedTypeSymbol : NamedTypeSymbol {
    private static readonly Dictionary<ReadOnlyMemory<char>, ImmutableArray<PENamedTypeSymbol>> EmptyNestedTypes =
        new Dictionary<ReadOnlyMemory<char>, ImmutableArray<PENamedTypeSymbol>>(EmptyReadOnlyMemoryOfCharComparer.Instance);

    private static readonly UncommonProperties NoUncommonProperties = new UncommonProperties();

    private readonly NamespaceOrTypeSymbol _container;
    private readonly TypeDefinitionHandle _handle;
    private readonly string _name;
    private readonly TypeAttributes _flags;

    private ICollection<string> _lazyMemberNames;
    private ImmutableArray<Symbol> _lazyMembersInDeclarationOrder;
    private Dictionary<string, ImmutableArray<Symbol>> _lazyMembersByName;
    private Dictionary<ReadOnlyMemory<char>, ImmutableArray<PENamedTypeSymbol>> _lazyNestedTypes;
    private TypeKind _lazyKind;

    private NullableContextKind _lazyNullableContextValue;

    private NamedTypeSymbol _lazyBaseType = ErrorTypeSymbol.UnknownResultType;
    private NamedTypeSymbol _lazyDeclaredBaseType = ErrorTypeSymbol.UnknownResultType;
    private UncommonProperties _lazyUncommonProperties;

    private PENamedTypeSymbol(
        PEModuleSymbol moduleSymbol,
        NamespaceOrTypeSymbol container,
        TypeDefinitionHandle handle,
        string emittedNamespaceName,
        ushort arity,
        out bool mangleName) {
        string metadataName;

        try {
            metadataName = moduleSymbol.module.GetTypeDefNameOrThrow(handle);
        } catch (BadImageFormatException) {
            metadataName = "";
        }

        _handle = handle;
        _container = container;

        try {
            _flags = moduleSymbol.module.GetTypeDefFlagsOrThrow(handle);
        } catch (BadImageFormatException) { }

        if (arity == 0) {
            _name = metadataName;
            mangleName = false;
        } else {
            _name = MetadataHelpers.UnmangleMetadataNameForArity(metadataName, arity);
            mangleName = !ReferenceEquals(_name, metadataName);
        }

        if (container.isNamespace && GeneratedNameParser.TryParseFileTypeName(
            _name,
            out var displayFileName,
            out var ordinal,
            out var originalTypeName)) {
            _name = originalTypeName;
        }
    }

    internal static PENamedTypeSymbol Create(
        PEModuleSymbol moduleSymbol,
        PENamespaceSymbol containingNamespace,
        TypeDefinitionHandle handle,
        string emittedNamespaceName) {
        GetGenericInfo(moduleSymbol, handle, out var genericParameterHandles, out var arity, out var mrEx);
        PENamedTypeSymbol result;

        if (arity == 0) {
            result = new PENamedTypeSymbolNonGeneric(moduleSymbol, containingNamespace, handle, emittedNamespaceName);
        } else {
            result = new PENamedTypeSymbolGeneric(
                moduleSymbol,
                containingNamespace,
                handle,
                emittedNamespaceName,
                genericParameterHandles,
                arity
            );
        }

        return result;
    }

    internal static PENamedTypeSymbol Create(
        PEModuleSymbol moduleSymbol,
        PENamedTypeSymbol containingType,
        TypeDefinitionHandle handle) {

        GetGenericInfo(moduleSymbol, handle, out var genericParameterHandles, out var metadataArity, out var mrEx);

        ushort arity = 0;
        var containerMetadataArity = containingType.metadataArity;

        if (metadataArity > containerMetadataArity)
            arity = (ushort)(metadataArity - containerMetadataArity);

        PENamedTypeSymbol result;

        if (metadataArity == 0) {
            result = new PENamedTypeSymbolNonGeneric(moduleSymbol, containingType, handle, null);
        } else {
            result = new PENamedTypeSymbolGeneric(
                moduleSymbol,
                containingType,
                handle,
                null,
                genericParameterHandles,
                arity
            );
        }

        return result;
    }

    public override string name => _name;

    public abstract override int arity { get; }

    public override TypeKind typeKind {
        get {
            var result = _lazyKind;

            if (result == TypeKind.Unknown) {
                TypeSymbol @base = GetDeclaredBaseType(skipTransformsIfNecessary: true);
                result = @base is null ? TypeKind.Struct : TypeKind.Class;
                _lazyKind = result;
            }

            return result;
        }
    }

    public override ImmutableArray<TypeOrConstant> templateArguments => [];

    public override ImmutableArray<TemplateParameterSymbol> templateParameters => [];

    public override ImmutableArray<BoundExpression> templateConstraints => [];

    internal abstract int metadataArity { get; }

    internal TypeDefinitionHandle handle => _handle;

    internal PEModuleSymbol containingPEModule {
        get {
            Symbol s = _container;

            while (s.kind != SymbolKind.Namespace)
                s = s.containingSymbol;

            return ((PENamespaceSymbol)s).containingPEModule;
        }
    }

    internal ModuleSymbol containingModule => containingPEModule;

    internal abstract override bool mangleName { get; }

    internal override ImmutableArray<TextLocation> locations
        => containingPEModule.metadataLocation.Cast<MetadataLocation, TextLocation>();

    internal override SyntaxReference syntaxReference => null;

    internal override TextLocation location => locations[0];

    internal override ImmutableArray<SyntaxReference> declaringSyntaxReferences => [];

    internal override bool isStatic => (_flags & TypeAttributes.Sealed) != 0 && (_flags & TypeAttributes.Abstract) != 0;

    internal override bool isAbstract => (_flags & TypeAttributes.Abstract) != 0 && (_flags & TypeAttributes.Sealed) == 0;

    internal bool isMetadataAbstract => (_flags & TypeAttributes.Abstract) != 0;

    internal override bool isSealed => (_flags & TypeAttributes.Sealed) != 0 && (_flags & TypeAttributes.Abstract) == 0;

    internal bool isMetadataSealed => (_flags & TypeAttributes.Sealed) != 0;

    internal TypeAttributes flags => _flags;

    internal override NamedTypeSymbol constructedFrom => this;

    internal override Symbol containingSymbol => _container;

    internal override NamedTypeSymbol containingType => _container as NamedTypeSymbol;

    internal override NamedTypeSymbol baseType {
        get {
            if (ReferenceEquals(_lazyBaseType, ErrorTypeSymbol.UnknownResultType)) {
                Interlocked.CompareExchange(
                    ref _lazyBaseType,
                    MakeAcyclicBaseType(),
                    ErrorTypeSymbol.UnknownResultType
                );
            }

            return _lazyBaseType;
        }
    }

    internal override Accessibility declaredAccessibility {
        get {
            var access = Accessibility.Private;

            access = (_flags & TypeAttributes.VisibilityMask) switch {
                TypeAttributes.NestedAssembly => Accessibility.Private,// access = Accessibility.Internal;
                TypeAttributes.NestedFamORAssem => Accessibility.Private,// access = Accessibility.ProtectedOrInternal;
                TypeAttributes.NestedFamANDAssem => Accessibility.Private,// access = Accessibility.ProtectedAndInternal;
                TypeAttributes.NestedPrivate => Accessibility.Private,
                TypeAttributes.Public or TypeAttributes.NestedPublic => Accessibility.Public,
                TypeAttributes.NestedFamily => Accessibility.Protected,
                TypeAttributes.NotPublic => Accessibility.Private,// access = Accessibility.Internal;
                _ => throw ExceptionUtilities.UnexpectedValue(_flags & TypeAttributes.VisibilityMask),
            };

            return access;
        }
    }

    internal override IEnumerable<string> memberNames {
        get {
            EnsureNonTypeMemberNamesAreLoaded();
            return _lazyMemberNames;
        }
    }

    internal override bool isRefLikeType {
        get {
            var uncommon = GetUncommonProperties();

            if (uncommon == NoUncommonProperties)
                return false;

            if (!uncommon.lazyIsByRefLike.HasValue()) {
                var isByRefLike = ThreeState.False;

                if (typeKind == TypeKind.Struct) {
                    var moduleSymbol = containingPEModule;
                    var module = moduleSymbol.module;
                    isByRefLike = module.HasIsByRefLikeAttribute(_handle).ToThreeState();
                }

                uncommon.lazyIsByRefLike = isByRefLike;
            }

            return uncommon.lazyIsByRefLike.Value();
        }
    }

    private UncommonProperties GetUncommonProperties() {
        var result = _lazyUncommonProperties;

        if (result is not null)
            return result;

        if (IsUncommon()) {
            result = new UncommonProperties();
            return Interlocked.CompareExchange(ref _lazyUncommonProperties, result, null) ?? result;
        }

        _lazyUncommonProperties = result = NoUncommonProperties;
        return result;
    }

    private bool IsUncommon() {
        if (containingPEModule.HasAnyCustomAttributes(_handle))
            return true;

        return false;
    }

    internal override ImmutableArray<Symbol> GetSimpleNonTypeMembers(string name) {
        EnsureAllMembersAreLoaded();

        if (!_lazyMembersByName.TryGetValue(name, out var m))
            m = [];

        return m;
    }

    internal override ImmutableArray<Symbol> GetMembers(string name) {
        EnsureAllMembersAreLoaded();

        if (!_lazyMembersByName.TryGetValue(name, out var m))
            m = [];

        if (_lazyNestedTypes.TryGetValue(name.AsMemory(), out var t))
            m = m.Concat(StaticCast<Symbol>.From(t));

        return m;
    }

    internal override ImmutableArray<Symbol> GetMembers() {
        EnsureAllMembersAreLoaded();
        return _lazyMembersInDeclarationOrder;
    }

    internal override ImmutableArray<NamedTypeSymbol> GetTypeMembersUnordered() {
        return GetTypeMembers();
    }

    internal override ImmutableArray<NamedTypeSymbol> GetTypeMembers() {
        EnsureNestedTypesAreLoaded();
        return GetMemberTypesPrivate();
    }

    private void EnsureAllMembersAreLoaded() {
        if (_lazyMembersByName is null)
            LoadMembers();
    }

    private ImmutableArray<NamedTypeSymbol> GetMemberTypesPrivate() {
        var builder = ArrayBuilder<NamedTypeSymbol>.GetInstance();

        foreach (var typeArray in _lazyNestedTypes.Values)
            builder.AddRange(typeArray);

        return builder.ToImmutableAndFree();
    }

    private void EnsureNestedTypesAreLoaded() {
        if (_lazyNestedTypes is null) {
            var types = ArrayBuilder<PENamedTypeSymbol>.GetInstance();
            types.AddRange(CreateNestedTypes());
            var typesDict = GroupByName(types);

            var exchangeResult = Interlocked.CompareExchange(ref _lazyNestedTypes, typesDict, null);

            if (exchangeResult is null) {
                var moduleSymbol = containingPEModule;
                moduleSymbol.OnNewTypeDeclarationsLoaded(typesDict);
            }

            types.Free();
        }
    }

    internal override ImmutableArray<NamedTypeSymbol> GetTypeMembers(ReadOnlyMemory<char> name) {
        EnsureNestedTypesAreLoaded();

        if (_lazyNestedTypes.TryGetValue(name, out var t))
            return StaticCast<NamedTypeSymbol>.From(t);

        return [];
    }

    internal override ImmutableArray<NamedTypeSymbol> GetTypeMembers(ReadOnlyMemory<char> name, int arity) {
        return GetTypeMembers(name).WhereAsArray((type, arity) => type.arity == arity, arity);
    }

    private static ExtendedErrorTypeSymbol CyclicInheritanceError(TypeSymbol declaredBase) {
        // var info = new CSDiagnosticInfo(ErrorCode.ERR_ImportedCircularBase, declaredBase);
        // TODO error
        return new ExtendedErrorTypeSymbol(declaredBase, LookupResultKind.NotReferencable, null, true);
    }

    private NamedTypeSymbol MakeAcyclicBaseType() {
        var declaredBase = GetDeclaredBaseType(null);

        if (declaredBase is null)
            return null;

        if (BaseTypeAnalysis.TypeDependsOn(declaredBase, this))
            return CyclicInheritanceError(declaredBase);

        SetKnownToHaveNoDeclaredBaseCycles();
        return declaredBase;
    }

    private static void GetGenericInfo(
        PEModuleSymbol moduleSymbol,
        TypeDefinitionHandle handle,
        out GenericParameterHandleCollection genericParameterHandles,
        out ushort arity,
        out BadImageFormatException mrEx) {
        try {
            genericParameterHandles = moduleSymbol.module.GetTypeDefGenericParamsOrThrow(handle);
            arity = (ushort)genericParameterHandles.Count;
            mrEx = null;
        } catch (BadImageFormatException e) {
            arity = 0;
            genericParameterHandles = default;
            mrEx = e;
        }
    }

    internal override NamedTypeSymbol GetDeclaredBaseType(ConsList<TypeSymbol> basesBeingResolved) {
        return GetDeclaredBaseType(skipTransformsIfNecessary: false);
    }

    internal override byte? GetNullableContextValue() {
        if (!_lazyNullableContextValue.TryGetByte(out var value)) {
            value = containingPEModule.module.HasNullableContextAttribute(_handle, out byte arg)
                ? arg
                : _container.GetNullableContextValue();

            _lazyNullableContextValue = value.ToNullableContextFlags();
        }

        return value;
    }

    internal override byte? GetLocalNullableContextValue() {
        throw ExceptionUtilities.Unreachable();
    }

    private NamedTypeSymbol GetDeclaredBaseType(bool skipTransformsIfNecessary) {
        if (ReferenceEquals(_lazyDeclaredBaseType, ErrorTypeSymbol.UnknownResultType)) {
            var baseType = MakeDeclaredBaseType();

            if (baseType is not null) {
                if (skipTransformsIfNecessary)
                    return baseType;

                var moduleSymbol = containingPEModule;
                // TypeSymbol decodedType = DynamicTypeDecoder.TransformType(baseType, 0, _handle, moduleSymbol);
                // decodedType = NativeIntegerTypeDecoder.TransformType(decodedType, _handle, moduleSymbol, this);
                // decodedType = TupleTypeDecoder.DecodeTupleTypesIfApplicable(decodedType, _handle, moduleSymbol);

                baseType = (NamedTypeSymbol)NullableTypeDecoder.TransformType(
                    // new TypeWithAnnotations(decodedType),
                    new TypeWithAnnotations(baseType),
                    _handle,
                    moduleSymbol,
                    accessSymbol: this,
                    nullableContext: this
                ).type;
            }

            Interlocked.CompareExchange(ref _lazyDeclaredBaseType, baseType, ErrorTypeSymbol.UnknownResultType);
        }

        return _lazyDeclaredBaseType;
    }

    private NamedTypeSymbol MakeDeclaredBaseType() {
        try {
            var moduleSymbol = containingPEModule;
            var token = moduleSymbol.module.GetBaseTypeOfTypeOrThrow(_handle);

            if (!token.IsNil)
                return (NamedTypeSymbol)new MetadataDecoder(moduleSymbol, this).GetTypeOfToken(token);
        } catch (BadImageFormatException mrEx) {
            return new UnsupportedMetadataTypeSymbol(mrEx);
        }

        return null;
    }

    private void LoadMembers() {
        ArrayBuilder<Symbol> members = null;

        if (_lazyMembersInDeclarationOrder.IsDefault) {
            EnsureNestedTypesAreLoaded();

            members = ArrayBuilder<Symbol>.GetInstance();

            var fieldMembers = ArrayBuilder<PEFieldSymbol>.GetInstance();
            var nonFieldMembers = ArrayBuilder<Symbol>.GetInstance();

            var privateFieldNameToSymbols = CreateFields(fieldMembers);
            var methodHandleToSymbol = CreateMethods(nonFieldMembers);

            if (typeKind == TypeKind.Struct) {
                var haveParameterlessConstructor = false;

                foreach (var method in nonFieldMembers.Cast<MethodSymbol>()) {
                    if (method.IsParameterlessConstructor()) {
                        haveParameterlessConstructor = true;
                        break;
                    }
                }

                if (!haveParameterlessConstructor)
                    nonFieldMembers.Insert(0, new SynthesizedConstructorSymbol(this));
            }

            foreach (var field in fieldMembers)
                members.Add(field);

            members.AddRange(nonFieldMembers);

            nonFieldMembers.Free();
            fieldMembers.Free();

            methodHandleToSymbol.Free();

            var membersCount = members.Count;

            foreach (var typeArray in _lazyNestedTypes.Values)
                members.AddRange(typeArray);

            members.Sort(membersCount, DeclarationOrderTypeSymbolComparer.Instance);
            var membersInDeclarationOrder = members.ToImmutable();

            if (!ImmutableInterlocked.InterlockedInitialize(ref _lazyMembersInDeclarationOrder, membersInDeclarationOrder)) {
                members.Free();
                members = null;
            } else {
                members.Clip(membersCount);
            }
        }

        if (_lazyMembersByName is null) {
            if (members is null) {
                members = ArrayBuilder<Symbol>.GetInstance();

                foreach (var member in _lazyMembersInDeclarationOrder) {
                    if (member.kind == SymbolKind.NamedType)
                        break;

                    members.Add(member);
                }
            }

            var membersDict = GroupByName(members);

            var exchangeResult = Interlocked.CompareExchange(ref _lazyMembersByName, membersDict, null);

            if (exchangeResult is null) {
                var memberNames = SpecializedCollections.ReadOnlyCollection(membersDict.Keys);
                Interlocked.Exchange(ref _lazyMemberNames, memberNames);
            }
        }

        members?.Free();
    }

    private static Dictionary<string, ImmutableArray<Symbol>> GroupByName(ArrayBuilder<Symbol> symbols) {
        return symbols.ToDictionary(s => s.name, StringOrdinalComparer.Instance);
    }

    private static Dictionary<ReadOnlyMemory<char>, ImmutableArray<PENamedTypeSymbol>> GroupByName(
        ArrayBuilder<PENamedTypeSymbol> symbols) {
        if (symbols.Count == 0)
            return EmptyNestedTypes;

        return symbols.ToDictionary(s => s.name.AsMemory(), ReadOnlyMemoryOfCharComparer.Instance);
    }

    private IEnumerable<PENamedTypeSymbol> CreateNestedTypes() {
        var moduleSymbol = containingPEModule;
        var module = moduleSymbol.module;

        ImmutableArray<TypeDefinitionHandle> nestedTypeDefs;

        try {
            nestedTypeDefs = module.GetNestedTypeDefsOrThrow(_handle);
        } catch (BadImageFormatException) {
            yield break;
        }

        foreach (var typeRid in nestedTypeDefs)
            yield return Create(moduleSymbol, this, typeRid);
    }

    private MultiDictionary<string, PEFieldSymbol> CreateFields(ArrayBuilder<PEFieldSymbol> fieldMembers) {
        var privateFieldNameToSymbols = new MultiDictionary<string, PEFieldSymbol>();

        var moduleSymbol = containingPEModule;
        var module = moduleSymbol.module;

        var isOrdinaryStruct = false;
        var isOrdinaryEmbeddableStruct = false;

        if (typeKind == TypeKind.Struct) {
            if (specialType == SpecialType.None)
                isOrdinaryEmbeddableStruct = containingAssembly.isLinked;

            switch (specialType) {
                case SpecialType.Void:
                case SpecialType.Bool:
                case SpecialType.Char:
                case SpecialType.Int:
                case SpecialType.Decimal:
                    isOrdinaryStruct = false;
                    break;
                default:
                    isOrdinaryStruct = true;
                    break;
            }
        }

        try {
            foreach (var fieldRid in module.GetFieldsOfTypeOrThrow(_handle)) {
                try {
                    if (!(isOrdinaryEmbeddableStruct ||
                        (isOrdinaryStruct && (module.GetFieldDefFlagsOrThrow(fieldRid) & FieldAttributes.Static) == 0)
                            || module.ShouldImportField(fieldRid, moduleSymbol.importOptions))) {
                        continue;
                    }
                } catch (BadImageFormatException) { }

                var symbol = new PEFieldSymbol(moduleSymbol, this, fieldRid);
                fieldMembers.Add(symbol);

                if (symbol.declaredAccessibility == Accessibility.Private) {
                    var name = symbol.name;

                    if (name.Length > 0)
                        privateFieldNameToSymbols.Add(name, symbol);
                }
            }
        } catch (BadImageFormatException) { }

        return privateFieldNameToSymbols;
    }

    private PooledDictionary<MethodDefinitionHandle, PEMethodSymbol> CreateMethods(ArrayBuilder<Symbol> members) {
        var moduleSymbol = containingPEModule;
        var module = moduleSymbol.module;
        var map = PooledDictionary<MethodDefinitionHandle, PEMethodSymbol>.GetInstance();

        var isOrdinaryEmbeddableStruct = (typeKind == TypeKind.Struct) &&
            (specialType == SpecialType.None) &&
            containingAssembly.isLinked;

        try {
            foreach (var methodHandle in module.GetMethodsOfTypeOrThrow(_handle)) {
                if (isOrdinaryEmbeddableStruct ||
                    module.ShouldImportMethod(_handle, methodHandle, moduleSymbol.importOptions)) {
                    var method = new PEMethodSymbol(moduleSymbol, this, methodHandle);
                    members.Add(method);
                    map.Add(methodHandle, method);
                }
            }
        } catch (BadImageFormatException) { }

        return map;
    }

    private void EnsureNonTypeMemberNamesAreLoaded() {
        if (_lazyMemberNames is null) {
            var moduleSymbol = containingPEModule;
            var module = moduleSymbol.module;

            var names = new HashSet<string>();

            try {
                foreach (var methodDef in module.GetMethodsOfTypeOrThrow(_handle)) {
                    try {
                        names.Add(module.GetMethodDefNameOrThrow(methodDef));
                    } catch (BadImageFormatException) { }
                }
            } catch (BadImageFormatException) { }

            try {
                foreach (var fieldDef in module.GetFieldsOfTypeOrThrow(_handle)) {
                    try {
                        names.Add(module.GetFieldDefNameOrThrow(fieldDef));
                    } catch (BadImageFormatException) { }
                }
            } catch (BadImageFormatException) { }

            if (isPrimitiveType)
                names.Add(WellKnownMemberNames.InstanceConstructorName);

            Interlocked.CompareExchange(ref _lazyMemberNames, CreateReadOnlyMemberNames(names), null);
        }
    }

    private static ICollection<string> CreateReadOnlyMemberNames(HashSet<string> names) {
        return names.Count switch {
            0 => SpecializedCollections.EmptySet<string>(),
            1 => (ICollection<string>)SpecializedCollections.SingletonCollection(names.First()),
            2 or 3 or 4 or 5 or 6 => [.. names],
            _ => SpecializedCollections.ReadOnlySet(names),
        };
    }
}
