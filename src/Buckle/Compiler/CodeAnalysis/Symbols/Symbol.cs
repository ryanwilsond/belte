using Buckle.CodeAnalysis.Display;

namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// A base symbol.
/// </summary>
internal abstract class Symbol : ISymbol {
    private protected Symbol(string name) {
        this.name = name;
    }

    /// <summary>
    /// Name of the symbol.
    /// </summary>
    public string name { get; }

    /// <summary>
    /// The type that contains this symbol, or null if nothing is containing this symbol.
    /// </summary>
    public virtual NamedTypeSymbol containingType { get; }

    /// <summary>
    /// The type of symbol this is (see <see cref="SymbolKind" />).
    /// </summary>
    public abstract SymbolKind kind { get; }

    public override string ToString() {
        return SymbolDisplay.DisplaySymbol(this).ToString();
    }

    public override int GetHashCode() {
        return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);
    }

    public override bool Equals(object obj) {
        return Equals(obj as Symbol);
    }

    public bool Equals(Symbol other) {
        return (object)this == other;
    }

    public static bool operator ==(Symbol left, Symbol right) {
        if (right is null)
            return left is null;

        return (object)left == (object)right || right.Equals(left);
    }

    public static bool operator !=(Symbol left, Symbol right) {
        if (right is null)
            return left is object;

        return (object)left != (object)right && !right.Equals(left);
    }
}
