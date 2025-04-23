using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;

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

    public override RefKind refKind { get; }

    public override int ordinal { get; }

    internal override Symbol containingSymbol { get; }

    internal override TypeWithAnnotations typeWithAnnotations { get; }

    internal override SyntaxReference syntaxReference => null;

    internal override TextLocation location => null;

    internal override bool isMetadataOptional => explicitDefaultConstantValue is not null;

    internal override ConstantValue explicitDefaultConstantValue => null;

    internal sealed override ScopedKind effectiveScope { get; }

    internal override bool isImplicitlyDeclared => true;

    // TODO Once actually added, complex/simple implement this separately
    internal override bool hasUnscopedRefAttribute => false;
}
