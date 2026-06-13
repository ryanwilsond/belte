using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class SourceComplexParameterSymbol : SourceComplexParameterSymbolBase {
    internal SourceComplexParameterSymbol(
        Symbol owner,
        int ordinal,
        TypeWithAnnotations type,
        RefKind refKind,
        bool isConst,
        string name,
        ParameterSyntax syntax,
        ScopedKind scope)
        : base(owner, ordinal, refKind, isConst, name, syntax, scope) {
        typeWithAnnotations = type;
    }

    internal override TypeWithAnnotations typeWithAnnotations { get; }
}
