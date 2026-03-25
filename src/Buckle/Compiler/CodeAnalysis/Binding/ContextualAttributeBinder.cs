using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

internal sealed class ContextualAttributeBinder : Binder {
    public ContextualAttributeBinder(Binder enclosing, Symbol symbol)
        : base(enclosing, enclosing.flags | BinderFlags.InContextualAttributeBinder) {
        attributeTarget = symbol;
        attributedMember = GetAttributedMember(symbol);
    }

    internal Symbol attributedMember { get; }

    internal Symbol attributeTarget { get; }

    internal static Symbol GetAttributedMember(Symbol symbol) {
        for (; symbol is not null; symbol = symbol.containingSymbol) {
            switch (symbol.kind) {
                case SymbolKind.Method:
                    return symbol;
            }
        }

        return symbol;
    }
}
