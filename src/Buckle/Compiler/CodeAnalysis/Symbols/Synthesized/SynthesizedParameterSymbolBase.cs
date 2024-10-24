using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class SynthesizedParameterSymbolBase : ParameterSymbol {
    internal SynthesizedParameterSymbolBase(
        Symbol container,
        TypeWithAnnotations type,
        int ordinal,
        RefKind refKind,
        ScopedKind scope,
        string name) {
        containingSymbol = container;
        typeWithAnnotations = type;
        this.ordinal = ordinal;
        this.refKind = refKind;
        effectiveScope = scope;
        this.name = name;
    }

    public override string name { get; }

    internal override Symbol containingSymbol { get; }

    internal override TypeWithAnnotations typeWithAnnotations { get; }

    internal override RefKind refKind { get; }

    internal override int ordinal { get; }

    internal override SyntaxReference syntaxReference => null;

    internal override bool isMetadataOptional => explicitDefaultConstantValue is not null;

    internal override ConstantValue explicitDefaultConstantValue => null;

    internal sealed override ScopedKind effectiveScope { get; }
}
