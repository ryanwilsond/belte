using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class SourceComplexParameterSymbolBase : SourceParameterSymbol {
    private protected ConstantValue _lazyDefaultSyntaxValue;

    private protected SourceComplexParameterSymbolBase(
        Symbol owner,
        int ordinal,
        RefKind refKind,
        string name,
        ParameterSyntax syntax,
        ScopedKind scope)
        : base(owner, ordinal, refKind, scope, name, syntax) { }


}
