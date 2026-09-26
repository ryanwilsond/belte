using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class SourceComplexParameterSymbol : SourceComplexParameterSymbolBase {
    internal SourceComplexParameterSymbol(
        Symbol owner,
        int ordinal,
        TypeWithAnnotations type,
        RefKind refKind,
        bool isConst,
        bool isConstExpr,
        string name,
        ParameterSyntax syntax,
        ScopedKind scope)
        : base(owner, ordinal, refKind, isConst, isConstExpr, name, syntax, syntax.identifier.location, scope) {
        typeWithAnnotations = type;
        AfterTypeChecks();
    }

    internal override TypeWithAnnotations typeWithAnnotations { get; }
}
