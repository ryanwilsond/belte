using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Threading;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Lowering;

internal sealed class SynthesizedTemplateType : WrappedNamedTypeSymbol, ISynthesizedTemplate {
    private readonly TemplateExpander _templateExpander;
    private readonly ConstructedNamedTypeSymbol _originalType;
    private readonly Dictionary<TemplateParameterSymbol, TemplateParameterSymbol> _replacementTemplateParameters;

    private ImmutableArray<Symbol> _lazyAllMembers;
    private Dictionary<(SynthesizedTemplateType, FieldSymbol), SynthesizedTemplateTypeField> _lazyFieldMap;

    private Dictionary<ReadOnlyMemory<char>, ImmutableArray<Symbol>> _nameToMembersMap;
    private Dictionary<ReadOnlyMemory<char>, ImmutableArray<NamedTypeSymbol>> _nameToTypeMembersMap;
    private int _hashCode;

    internal SynthesizedTemplateType(
        TemplateExpander templateExpander,
        Symbol containingSymbol,
        ConstructedNamedTypeSymbol originalType)
        : base(originalType.constructedFrom, null) {
        _originalType = originalType;
        _templateExpander = templateExpander;

        name = GeneratedNames.MakeTemplateTypeOrMethodName(originalType);

        var i = 0;

        Debug.Assert(originalType.templateParameters.Length == originalType.arity);
        var newTemplatesBuilder = ArrayBuilder<TemplateParameterSymbol>.GetInstance(originalType.arity);

        for (var j = 0; j < originalType.arity; j++) {
            var parameter = originalType.templateParameters[j];
            var argument = originalType.templateArguments[j];

            if (parameter.underlyingType.specialType == SpecialType.Type &&
                !parameter.isCompileTimeType &&
                !argument.isTemplateSpecializedType) {
                newTemplatesBuilder.Add(new SynthesizedTemplateTypeParameter(this, parameter, i++));
            }
        }

        templateParameters = newTemplatesBuilder.ToImmutableAndFree();

        i = 0;
        templateSubstitution = new TemplateMap(
            originalType.constructedFrom.containingType,
            originalType.templateParameters,
            originalType.templateArguments.ZipAsArray(
                originalType.constructedFrom.templateParameters,
                i,
                (typeOrConstant, templateParameter, i, arg) => {
                    if (templateParameter.underlyingType.specialType == SpecialType.Type &&
                        !templateParameter.isCompileTimeType &&
                        !typeOrConstant.isTemplateSpecializedType) {
                        return new TypeOrConstant(templateParameters[templateParameter.ordinal - i]);
                    } else {
                        i++;
                        return typeOrConstant;
                    }
                }
            )
        );

        _replacementTemplateParameters = [];

        i = 0;
        for (var j = 0; j < originalType.constructedFrom.templateParameters.Length; j++) {
            var parameter = originalType.constructedFrom.templateParameters[j];
            var argument = originalType.templateArguments[j];

            if (parameter.underlyingType.specialType == SpecialType.Type &&
                !parameter.isCompileTimeType &&
                !argument.isTemplateSpecializedType) {
                _replacementTemplateParameters.Add(parameter, templateParameters[i++]);
            }
        }

        this.containingSymbol = containingSymbol;
    }

    public override string name { get; }

    public override ImmutableArray<TemplateParameterSymbol> templateParameters { get; }

    public override ImmutableArray<TypeOrConstant> templateArguments => GetTemplateParametersAsTemplateArguments();

    public override ImmutableArray<BoundExpression> templateConstraints => underlyingNamedType.templateConstraints;

    public override int arity => templateParameters.Length;

    public override TemplateMap templateSubstitution { get; }

    internal override NamedTypeSymbol originalDefinition => this;

    internal override NamedTypeSymbol baseType => underlyingNamedType.baseType;

    internal override NamedTypeSymbol constructedFrom => this;

    internal override Symbol containingSymbol { get; }

    internal override IEnumerable<string> memberNames => [];

    internal ConstructedNamedTypeSymbol unexpandedSymbol => _originalType;

    internal Dictionary<(SynthesizedTemplateType, FieldSymbol), SynthesizedTemplateTypeField> fieldMap {
        get {
            if (_lazyFieldMap is null) {
                _ = GetMembers();
                Debug.Assert(_lazyFieldMap is not null);
                Debug.Assert(!_lazyAllMembers.IsDefault);
            }

            return _lazyFieldMap;
        }
    }

    internal Dictionary<TemplateParameterSymbol, TemplateParameterSymbol> replacementTemplateParameters
        => _replacementTemplateParameters;

    internal void NoteFields(Dictionary<(SynthesizedTemplateType, FieldSymbol), SynthesizedTemplateTypeField> builder) {
        foreach (var pair in fieldMap)
            builder.Add(pair.Key, pair.Value);
    }

    private ImmutableArray<Symbol> MakeMembers(TemplateExpander templateExpander) {
        _lazyFieldMap = [];
        var builder = ArrayBuilder<Symbol>.GetInstance();

        var unexpandedMembers = unexpandedSymbol.GetMembers();

        foreach (var member in unexpandedMembers) {
            switch (member.kind) {
                case SymbolKind.Field:
                    var originalField = ((FieldSymbol)member).originalDefinition;
                    var templateField = new SynthesizedTemplateTypeField(templateExpander, this, originalField);
                    _lazyFieldMap.Add((this, originalField), templateField);
                    builder.Add(templateField);
                    break;
                case SymbolKind.NamedType:
                    builder.Add(((NamedTypeSymbol)member).originalDefinition);
                    break;
                case SymbolKind.Method:
                    // Methods are checked using the BoundProgram method map anyways so it shouldn't matter that they are missing here
                    break;
                default:
                    throw ExceptionUtilities.UnexpectedValue(member.kind);
            }
        }

        return builder.ToImmutableAndFree();
    }

    internal override LexicalSortKey GetLexicalSortKey() {
        return LexicalSortKey.NotInSource;
    }

    internal override NamedTypeSymbol GetDeclaredBaseType(ConsList<TypeSymbol> basesBeingResolved) {
        return baseType;
    }

    internal override ImmutableArray<NamedTypeSymbol> GetDeclaredInterfaces(ConsList<TypeSymbol> basesBeingResolved) {
        return unexpandedSymbol.GetDeclaredInterfaces(basesBeingResolved);
    }

    internal override ImmutableArray<NamedTypeSymbol> Interfaces(ConsList<TypeSymbol> basesBeingResolved = null) {
        return unexpandedSymbol.Interfaces(basesBeingResolved);
    }

    internal override ImmutableArray<Symbol> GetMembers() {
        if (_lazyAllMembers.IsDefault)
            ImmutableInterlocked.InterlockedInitialize(ref _lazyAllMembers, MakeMembers(_templateExpander));

        return _lazyAllMembers;
    }

    internal override ImmutableArray<Symbol> GetEarlyAttributeDecodingMembers() {
        return GetMembersUnordered();
    }

    internal override ImmutableArray<Symbol> GetEarlyAttributeDecodingMembers(string name) {
        return GetMembers(name);
    }

    internal override ImmutableArray<Symbol> GetMembers(string name) {
        return GetNameToMembersMap().TryGetValue(name.AsMemory(), out var members) ? members : [];
    }

    internal override ImmutableArray<NamedTypeSymbol> GetTypeMembers() {
        return GetNameToTypeMembersMap().Flatten(LexicalOrderSymbolComparer.Instance);
    }

    internal override ImmutableArray<NamedTypeSymbol> GetTypeMembers(ReadOnlyMemory<char> name) {
        return GetNameToTypeMembersMap().TryGetValue(name, out var members) ? members : [];
    }

    internal sealed override IEnumerable<(MethodSymbol Body, MethodSymbol Implemented)> SynthesizedInterfaceMethodImpls() {
        return SpecializedCollections.EmptyEnumerable<(MethodSymbol Body, MethodSymbol Implemented)>();
    }

    private protected override NamedTypeSymbol WithTupleDataCore(TupleExtraData newData) {
        throw ExceptionUtilities.Unreachable();
    }

    public override int GetHashCode() {
        if (_hashCode == 0)
            _hashCode = ComputeHashCode();

        return _hashCode;
    }

    internal new int ComputeHashCode() {
        var baseHashCode = base.GetHashCode();
        var newHashCode = baseHashCode;

        for (var i = 0; i < _originalType.templateArguments.Length; i++) {
            var argument = _originalType.templateArguments[i];
            var parameter = _originalType.templateParameters[i];

            if (argument.isConstant || argument.isTemplateSpecializedType || parameter.isCompileTimeType)
                newHashCode = Hash.Combine(argument, newHashCode);
        }

        Debug.Assert(baseHashCode != newHashCode);
        return newHashCode;
    }

    // TODO This code is identical SynthesizedFinishedNamedTypeSymbol, SourceNamespaceSymbol, and SynthesizedBelteNamespaceSymbol
    // So consider refactoring this out
    private Dictionary<ReadOnlyMemory<char>, ImmutableArray<Symbol>> GetNameToMembersMap() {
        if (_nameToMembersMap is null)
            Interlocked.CompareExchange(ref _nameToMembersMap, MakeNameToMembersMap(), null);

        return _nameToMembersMap;
    }

    private Dictionary<ReadOnlyMemory<char>, ImmutableArray<NamedTypeSymbol>> GetNameToTypeMembersMap() {
        if (_nameToTypeMembersMap is null) {
            Interlocked.CompareExchange(
                ref _nameToTypeMembersMap,
                ImmutableArrayExtensions
                    .GetTypesFromMemberMap<ReadOnlyMemory<char>, Symbol, NamedTypeSymbol>(
                        GetNameToMembersMap(),
                        ReadOnlyMemoryOfCharComparer.Instance
                    ),
                null
            );
        }

        return _nameToTypeMembersMap;
    }

    private Dictionary<ReadOnlyMemory<char>, ImmutableArray<Symbol>> MakeNameToMembersMap() {
        var builder = NameToObjectPool.Allocate();

        foreach (var symbol in GetMembers()) {
            ImmutableArrayExtensions.AddToMultiValueDictionaryBuilder(
                builder,
                symbol.name.AsMemory(),
                symbol
            );
        }

        var result = new Dictionary<ReadOnlyMemory<char>, ImmutableArray<Symbol>>(
            builder.Count,
            ReadOnlyMemoryOfCharComparer.Instance
        );

        foreach (var pair in builder) {
            result.Add(pair.Key, pair.Value is ArrayBuilder<Symbol> arrayBuilder
                ? arrayBuilder.ToImmutableAndFree()
                : [(Symbol)pair.Value]);
        }

        builder.Free();
        return result;
    }

    ISymbolWithTemplates ISynthesizedTemplate.unexpandedSymbol => _originalType;

    Dictionary<TemplateParameterSymbol, TemplateParameterSymbol> ISynthesizedTemplate.replacementTemplateParameters
        => _replacementTemplateParameters;
}
