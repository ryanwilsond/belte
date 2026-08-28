using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Threading;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Display;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;
using TemplateTypeDecoder = Buckle.CodeAnalysis.TemplateMetadataReader.TemplateMetadata.TemplateTypeDecoder;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed partial class PETemplateType : NamedTypeSymbol {
    private static readonly Dictionary<ReadOnlyMemory<char>, ImmutableArray<NamedTypeSymbol>> EmptyNestedTypes =
        new Dictionary<ReadOnlyMemory<char>, ImmutableArray<NamedTypeSymbol>>(EmptyReadOnlyMemoryOfCharComparer.Instance);

    private readonly TemplateTypeDecoder _decoder;
    private readonly NamespaceOrTypeSymbol _container;
    private readonly string _name;
    private readonly TypeAttributes _flags;

    private ImmutableArray<TemplateParameterSymbol> _lazyTemplateParameters;
    private ImmutableArray<BoundExpression> _lazyTemplateConstraints;
    private ICollection<string> _lazyMemberNames;
    private NamedTypeSymbol _lazyBaseType = ErrorTypeSymbol.UnknownResultType;
    private NamedTypeSymbol _lazyDeclaredBaseType = ErrorTypeSymbol.UnknownResultType;
    private TypeKind _lazyKind;
    private ImmutableArray<NamedTypeSymbol> _lazyDeclaredInterfaces = default;
    private ImmutableArray<NamedTypeSymbol> _lazyInterfaces = default;
    private ImmutableArray<Symbol> _lazyMembersInDeclarationOrder;
    private Dictionary<string, ImmutableArray<Symbol>> _lazyMembersByName;
    private Dictionary<ReadOnlyMemory<char>, ImmutableArray<NamedTypeSymbol>> _lazyNestedTypes;

    private readonly NamedTypeSymbol _typeToLink;

    internal PETemplateType(NamespaceOrTypeSymbol container, TemplateTypeDecoder decoder) {
        decoder.SetEnclosingContext(this);
        _decoder = decoder;

        var metadataName = decoder.GetMetadataName();

        arity = decoder.GetArity();

        if (arity == 0)
            _name = metadataName;
        else
            _name = MetadataHelpers.UnmangleMetadataNameForArity(metadataName, arity);

        _container = container;
        _flags = decoder.GetTypeFlags();
    }

    internal PETemplateType(NamespaceOrTypeSymbol container, TemplateTypeDecoder decoder, NamedTypeSymbol typeToLink)
        : this(container, decoder) {
        _typeToLink = typeToLink;
    }

    public override string name => _name;

    public override int arity { get; }

    public override TypeKind typeKind {
        get {
            var result = _lazyKind;

            if (result == TypeKind.Unknown) {
                if ((_flags & TypeAttributes.Interface) != 0) {
                    result = TypeKind.Interface;
                } else {
                    TypeSymbol @base = GetDeclaredBaseType(skipTransformsIfNecessary: true);
                    result = TypeKind.Class;

                    if (@base is not null) {
                        var baseCorTypeId = @base.specialType;

                        switch (baseCorTypeId) {
                            case SpecialType.Enum:
                                throw ExceptionUtilities.Unreachable();
                            case SpecialType.ValueType:
                                if (specialType != SpecialType.Enum)
                                    result = TypeKind.Struct;

                                break;
                        }
                    }

                    if (@base?.ToDisplayString(SymbolDisplayFormat.NamespaceQualifiedNameFormat) == "System.Enum")
                        throw ExceptionUtilities.Unreachable();

                    if (@base?.ToDisplayString(SymbolDisplayFormat.NamespaceQualifiedNameFormat) == "System.ValueType")
                        result = TypeKind.Struct;
                }

                _lazyKind = result;
            }

            return result;
        }
    }

    internal override bool mangleName => true;

    internal override bool isRefLikeType => false;

    internal override NamedTypeSymbol originalDefinition => _typeToLink ?? base.originalDefinition;

    internal PEModuleSymbol containingPEModule {
        get {
            Symbol s = _container;

            while (s.kind != SymbolKind.Namespace)
                s = s.containingSymbol;

            return ((PENamespaceSymbol)s).containingPEModule;
        }
    }

    internal override ModuleSymbol containingModule => containingPEModule;

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

    internal sealed override bool isInterface => (_flags & TypeAttributes.Interface) != 0;

    internal override Accessibility declaredAccessibility {
        get {
            Accessibility access;

            access = (_flags & TypeAttributes.VisibilityMask) switch {
                TypeAttributes.NestedAssembly => Accessibility.Internal,
                TypeAttributes.NestedFamORAssem => Accessibility.InternalOrProtected,
                TypeAttributes.NestedFamANDAssem => Accessibility.InternalAndProtected,
                TypeAttributes.NestedPrivate => Accessibility.Private,
                TypeAttributes.Public or TypeAttributes.NestedPublic => Accessibility.Public,
                TypeAttributes.NestedFamily => Accessibility.Protected,
                TypeAttributes.NotPublic => Accessibility.Internal,
                _ => throw ExceptionUtilities.UnexpectedValue(_flags & TypeAttributes.VisibilityMask),
            };

            return access;
        }
    }

    public override ImmutableArray<BoundExpression> templateConstraints {
        get {
            EnsureTemplateConstraintsAreLoaded();
            return _lazyTemplateConstraints;
        }
    }

    public override ImmutableArray<TypeOrConstant> templateArguments => GetTemplateParametersAsTemplateArguments();

    public override ImmutableArray<TemplateParameterSymbol> templateParameters {
        get {
            EnsureTemplateParametersAreLoaded();
            return _lazyTemplateParameters;
        }
    }

    internal override IEnumerable<string> memberNames {
        get {
            EnsureNonTypeMemberNamesAreLoaded();
            return _lazyMemberNames;
        }
    }

    private NamedTypeSymbol GetDeclaredBaseType(bool skipTransformsIfNecessary) {
        if (ReferenceEquals(_lazyDeclaredBaseType, ErrorTypeSymbol.UnknownResultType)) {
            var baseType = MakeDeclaredBaseType();

            // TODO Transforms (nullability, tuples, etc.)

            Interlocked.CompareExchange(ref _lazyDeclaredBaseType, baseType, ErrorTypeSymbol.UnknownResultType);
        }

        return _lazyDeclaredBaseType;
    }

    private NamedTypeSymbol MakeDeclaredBaseType() {
        if (!_flags.IsInterface())
            return _decoder.GetBaseType();

        return null;
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

    private static ExtendedErrorTypeSymbol CyclicInheritanceError(TypeSymbol declaredBase) {
        // var info = new CSDiagnosticInfo(ErrorCode.ERR_ImportedCircularBase, declaredBase);
        // TODO error
        throw ExceptionUtilities.Unreachable();
        // return new ExtendedErrorTypeSymbol(declaredBase, LookupResultKind.NotReferencable, null, true);
    }

    private void EnsureNonTypeMemberNamesAreLoaded() {
        if (_lazyMemberNames is null) {
            var names = new HashSet<string>();

            names.AddAll(_decoder.GetMethodNames());
            names.AddAll(_decoder.GetFieldNames());

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

    private void EnsureTemplateParametersAreLoaded() {
        if (_lazyTemplateParameters.IsDefault) {
            var ownedParams = ArrayBuilder<TemplateParameterSymbol>.GetInstance(arity);
            ownedParams.Count = arity;

            for (var i = 0; i < ownedParams.Count; i++) {
                if (_typeToLink is null) {
                    ownedParams[i] = new MetadataTemplateParameterSymbol(_decoder, this, (ushort)i);
                } else {
                    ownedParams[i] = new MetadataTemplateParameterSymbol(
                        _decoder,
                        this,
                        (ushort)i,
                        _typeToLink.templateParameters[i]
                    );
                }
            }

            ImmutableInterlocked.InterlockedInitialize(
                ref _lazyTemplateParameters,
                ownedParams.ToImmutableAndFree()
            );
        }
    }

    private void EnsureTemplateConstraintsAreLoaded() {
        if (_lazyTemplateConstraints.IsDefault) {
            var constraints = _decoder.DecodeConstraints();
            ImmutableInterlocked.InterlockedInitialize(ref _lazyTemplateConstraints, constraints.ToImmutableArray());
        }
    }

    internal override ImmutableArray<NamedTypeSymbol> Interfaces(ConsList<TypeSymbol> basesBeingResolved = null) {
        if (_lazyInterfaces.IsDefault) {
            ImmutableInterlocked.InterlockedCompareExchange(
                ref _lazyInterfaces,
                MakeAcyclicInterfaces(),
                default
            );
        }

        return _lazyInterfaces;
    }

    private ImmutableArray<NamedTypeSymbol> MakeAcyclicInterfaces() {
        var declaredInterfaces = GetDeclaredInterfaces(null);

        if (!isInterface)
            return declaredInterfaces;

        return declaredInterfaces
            .SelectAsArray(t => BaseTypeAnalysis.TypeDependsOn(t, this) ? CyclicInheritanceError(t) : t);
    }

    internal override ImmutableArray<NamedTypeSymbol> GetDeclaredInterfaces(ConsList<TypeSymbol> basesBeingResolved) {
        if (_lazyDeclaredInterfaces.IsDefault) {
            ImmutableInterlocked.InterlockedCompareExchange(
                ref _lazyDeclaredInterfaces,
                MakeDeclaredInterfaces(),
                default
            );
        }

        return _lazyDeclaredInterfaces;
    }

    private ImmutableArray<NamedTypeSymbol> MakeDeclaredInterfaces() {
        return _decoder.DecodeInterfaces();
    }

    internal override ImmutableArray<AttributeData> GetAttributes() {
        // TODO
        return [];
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
            var types = ArrayBuilder<NamedTypeSymbol>.GetInstance();
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

    private void LoadMembers() {
        ArrayBuilder<Symbol> members = null;

        if (_lazyMembersInDeclarationOrder.IsDefault) {
            EnsureNestedTypesAreLoaded();

            members = ArrayBuilder<Symbol>.GetInstance();

            var fieldMembers = ArrayBuilder<FieldSymbol>.GetInstance();
            var nonFieldMembers = ArrayBuilder<Symbol>.GetInstance();

            CreateFields(fieldMembers);
            CreateMethods(nonFieldMembers);

            foreach (var field in fieldMembers)
                members.Add(field);

            members.AddRange(nonFieldMembers);

            nonFieldMembers.Free();
            fieldMembers.Free();

            var membersCount = members.Count;

            foreach (var typeArray in _lazyNestedTypes.Values)
                members.AddRange(typeArray);

            members.Sort(membersCount, PENamedTypeSymbol.DeclarationOrderTypeSymbolComparer.Instance);
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

    private static Dictionary<ReadOnlyMemory<char>, ImmutableArray<NamedTypeSymbol>> GroupByName(
        ArrayBuilder<NamedTypeSymbol> symbols) {
        if (symbols.Count == 0)
            return EmptyNestedTypes;

        return symbols.ToDictionary(s => s.name.AsMemory(), ReadOnlyMemoryOfCharComparer.Instance);
    }

    private IEnumerable<NamedTypeSymbol> CreateNestedTypes() {
        // TODO
        return [];
    }

    private void CreateFields(ArrayBuilder<FieldSymbol> fieldMembers) {
        var fieldInfos = _decoder.DecodeFields();

        foreach (var fieldInfo in fieldInfos) {
            MetadataFieldSymbol symbol;

            if (_typeToLink is null) {
                symbol = new MetadataFieldSymbol(
                    this,
                    fieldInfo.Item1,
                    fieldInfo.Item2,
                    fieldInfo.Item3,
                    fieldInfo.Item4
                );
            } else {
                symbol = new MetadataFieldSymbol(
                    this,
                    fieldInfo.Item1,
                    fieldInfo.Item2,
                    fieldInfo.Item3,
                    fieldInfo.Item4,
                    (FieldSymbol)_typeToLink.GetMembers(fieldInfo.Item1).Single(m => m.kind == SymbolKind.Field)
                );
            }

            fieldMembers.Add(symbol);
        }
    }

    private void CreateMethods(ArrayBuilder<Symbol> members) {
        var methodIndexes = _decoder.DecodeMethodIndexes();

        foreach (var index in methodIndexes) {
            var methodDecoder = _decoder.GetMethodDecoder(index);

            if (_typeToLink is null)
                members.Add(new MetadataMethodSymbol(this, methodDecoder));
            else
                members.Add(new MetadataMethodSymbol(this, methodDecoder, _typeToLink));
        }
    }

    private protected override NamedTypeSymbol WithTupleDataCore(TupleExtraData newData) {
        throw ExceptionUtilities.Unreachable();
    }

    internal override ImmutableArray<Symbol> GetEarlyAttributeDecodingMembers() {
        return GetMembersUnordered();
    }

    internal override ImmutableArray<Symbol> GetEarlyAttributeDecodingMembers(string name) {
        return GetMembers(name);
    }

    internal override AttributeUsageInfo GetAttributeUsageInfo() {
        // TODO Attributes
        return AttributeUsageInfo.Default;
    }

    internal sealed override IEnumerable<(MethodSymbol Body, MethodSymbol Implemented)> SynthesizedInterfaceMethodImpls() {
        return SpecializedCollections.EmptyEnumerable<(MethodSymbol Body, MethodSymbol Implemented)>();
    }

    internal override NamedTypeSymbol GetDeclaredBaseType(ConsList<TypeSymbol> basesBeingResolved) {
        return GetDeclaredBaseType(skipTransformsIfNecessary: false);
    }
}
