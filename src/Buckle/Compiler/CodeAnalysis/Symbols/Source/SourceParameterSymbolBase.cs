using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class SourceParameterSymbolBase : ParameterSymbol {
    internal SourceParameterSymbolBase(Symbol containingSymbol, int ordinal) {
        this.containingSymbol = containingSymbol;
        this.ordinal = ordinal;
    }

    internal sealed override Symbol containingSymbol { get; }

    internal sealed override int ordinal { get; }

    internal sealed override bool Equals(Symbol obj, TypeCompareKind compareKind) {
        if ((object)obj == this)
            return true;

        var symbol = obj as SourceParameterSymbolBase;

        return symbol is not null &&
            symbol.ordinal == ordinal &&
            symbol.containingSymbol.Equals(containingSymbol, compareKind);
    }

    public sealed override int GetHashCode() {
        return Hash.Combine(containingSymbol.GetHashCode(), ordinal);
    }
}
