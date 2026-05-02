using System.Collections.Immutable;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class WrappedParameterSymbol : ParameterSymbol {
    internal WrappedParameterSymbol(ParameterSymbol underlyingParameter) {
        this.underlyingParameter = underlyingParameter;
    }

    public sealed override string name => underlyingParameter.name;

    public sealed override string metadataName => underlyingParameter.metadataName;

    public sealed override RefKind refKind => underlyingParameter.refKind;

    public override int ordinal => underlyingParameter.ordinal;

    internal ParameterSymbol underlyingParameter { get; }

    internal sealed override SyntaxReference syntaxReference => underlyingParameter.syntaxReference;

    internal sealed override TextLocation location => underlyingParameter.location;

    internal sealed override ConstantValue explicitDefaultConstantValue
        => underlyingParameter.explicitDefaultConstantValue;

    internal sealed override ScopedKind effectiveScope => underlyingParameter.effectiveScope;

    internal override TypeWithAnnotations typeWithAnnotations => underlyingParameter.typeWithAnnotations;

    internal override bool isMetadataOptional => underlyingParameter.isMetadataOptional;

    internal override bool isImplicitlyDeclared => underlyingParameter.isImplicitlyDeclared;

    internal sealed override bool hasUnscopedRefAttribute => underlyingParameter.hasUnscopedRefAttribute;

    internal sealed override bool isMetadataOut => underlyingParameter.isMetadataOut;

    internal override ImmutableArray<AttributeData> GetAttributes() {
        return underlyingParameter.GetAttributes();
    }
}
