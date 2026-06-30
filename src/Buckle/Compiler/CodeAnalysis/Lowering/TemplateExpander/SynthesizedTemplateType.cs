using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Lowering;

internal sealed class SynthesizedTemplateType : WrappedNamedTypeSymbol, ISynthesizedTemplate<NamedTypeSymbol> {
    private readonly ConstructedNamedTypeSymbol _originalType;
    private readonly Dictionary<TemplateParameterSymbol, TemplateParameterSymbol> _replacementTemplateParameters;

    private int _hashCode;

    internal SynthesizedTemplateType(
        Symbol containingSymbol,
        ConstructedNamedTypeSymbol originalType)
        : base(originalType.constructedFrom, null) {
        _originalType = originalType;
        name = GeneratedNames.MakeTemplateTypeOrMethodName(originalType);

        var i = 0;
        templateParameters = originalType.templateParameters
            .Where(t => t.underlyingType.specialType == SpecialType.Type)
            .Select(t => new SynthesizedTemplateTypeParameter(this, t, i++))
            .ToImmutableArray<TemplateParameterSymbol>();

        i = 0;
        templateSubstitution = new TemplateMap(
            originalType.constructedFrom.containingType,
            originalType.templateParameters,
            originalType.templateArguments.ZipAsArray(
                originalType.constructedFrom.templateParameters,
                i,
                (typeOrConstant, templateParameter, i, arg) => {
                    if (templateParameter.underlyingType.specialType == SpecialType.Type) {
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
        foreach (var templateParameter in originalType.constructedFrom.templateParameters) {
            if (templateParameter.underlyingType.specialType == SpecialType.Type)
                _replacementTemplateParameters.Add(templateParameter, templateParameters[i++]);
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

    internal NamedTypeSymbol unexpandedSymbol => _originalType;

    internal Dictionary<TemplateParameterSymbol, TemplateParameterSymbol> replacementTemplateParameters
        => _replacementTemplateParameters;

    internal override LexicalSortKey GetLexicalSortKey() {
        return LexicalSortKey.NotInSource;
    }

    internal override NamedTypeSymbol GetDeclaredBaseType(ConsList<TypeSymbol> basesBeingResolved) {
        return baseType;
    }

    internal override ImmutableArray<NamedTypeSymbol> GetDeclaredInterfaces(ConsList<TypeSymbol> basesBeingResolved) {
        return [];
    }

    internal override ImmutableArray<NamedTypeSymbol> Interfaces(ConsList<TypeSymbol> basesBeingResolved = null) {
        return [];
    }

    internal override ImmutableArray<Symbol> GetMembers() {
        return [];
    }

    internal override ImmutableArray<Symbol> GetMembers(string name) {
        return [];
    }

    internal override ImmutableArray<NamedTypeSymbol> GetTypeMembers() {
        return [];
    }

    internal override ImmutableArray<NamedTypeSymbol> GetTypeMembers(ReadOnlyMemory<char> name) {
        return [];
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

        foreach (var templateArgument in _originalType.templateArguments) {
            if (templateArgument.isConstant)
                newHashCode = Hash.Combine(templateArgument.constant, newHashCode);
        }

        Debug.Assert(baseHashCode != newHashCode);
        return newHashCode;
    }

    NamedTypeSymbol ISynthesizedTemplate<NamedTypeSymbol>.unexpandedSymbol => _originalType;

    Dictionary<TemplateParameterSymbol, TemplateParameterSymbol> ISynthesizedTemplate<NamedTypeSymbol>.replacementTemplateParameters
        => _replacementTemplateParameters;
}
