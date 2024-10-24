using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class SourceComplexParameterSymbol : SourceComplexParameterSymbolBase {
    internal SourceComplexParameterSymbol(
        Symbol owner,
        int ordinal,
        TypeWithAnnotations type,
        RefKind refKind,
        string name,
        ParameterSyntax syntax,
        ScopedKind scope)
        : base(owner, ordinal, refKind, scope, name, syntax) {
        
    }

    internal override TypeWithAnnotations typeWithAnnotations { get; }
}
