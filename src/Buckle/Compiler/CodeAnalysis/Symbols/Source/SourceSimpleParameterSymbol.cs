using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class SourceSimpleParameterSymbol : SourceParameterSymbol {
    internal SourceSimpleParameterSymbol(
        Symbol owner,
        TypeWithAnnotations type,
        int ordinal,
        RefKind refKind,
        string name,
        ParameterSyntax syntax)
        : base(owner, ordinal, refKind, ScopedKind.None, name, syntax) {
        typeWithAnnotations = type;
    }

    internal override TypeWithAnnotations typeWithAnnotations { get; }

    internal override ConstantValue explicitDefaultConstantValue => null;

    internal override bool isMetadataOptional => false;

    internal override bool hasDefaultArgumentSyntax => false;
}
