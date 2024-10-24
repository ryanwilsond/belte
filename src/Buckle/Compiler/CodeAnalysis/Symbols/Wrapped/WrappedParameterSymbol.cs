using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class WrappedParameterSymbol : ParameterSymbol {
    internal WrappedParameterSymbol(ParameterSymbol underlyingParameter) {
        this.underlyingParameter = underlyingParameter;
    }

    public sealed override string name => underlyingParameter.name;

    public sealed override string metadataName => underlyingParameter.metadataName;

    internal ParameterSymbol underlyingParameter { get; }

    internal sealed override RefKind refKind => underlyingParameter.refKind;

    internal sealed override SyntaxReference syntaxReference => underlyingParameter.syntaxReference;

    internal sealed override ConstantValue explicitDefaultConstantValue
        => underlyingParameter.explicitDefaultConstantValue;

    internal sealed override ScopedKind effectiveScope => underlyingParameter.effectiveScope;

    internal override TypeWithAnnotations typeWithAnnotations => underlyingParameter.typeWithAnnotations;

    internal override int ordinal => underlyingParameter.ordinal;

    internal override bool isMetadataOptional => underlyingParameter.isMetadataOptional;

    internal override bool isImplicitlyDeclared => underlyingParameter.isImplicitlyDeclared;
}
