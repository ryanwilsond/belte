using Buckle.CodeAnalysis.Display;

namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// A base symbol.
/// </summary>
internal abstract class Symbol : ISymbol {
    private protected Symbol(string name) {
        this.name = name;
        accessibility = Accessibility.NotApplicable;
    }

    private protected Symbol(string name, Accessibility accessibility) {
        this.name = name;
        this.accessibility = accessibility;
    }

    /// <summary>
    /// Name of the symbol.
    /// </summary>
    public string name { get; }

    /// <summary>
    /// The accessibility/protection level of the symbol.
    /// </summary> <summary>
    public Accessibility accessibility { get; }

    /// <summary>
    /// The type that contains this symbol, or null if nothing is containing this symbol.
    /// </summary>
    public virtual NamedTypeSymbol containingType { get; private set; }

    /// <summary>
    /// Gets the original definition of the symbol.
    /// </summary>
    public Symbol originalDefinition => originalSymbolDefinition;

    public virtual Symbol originalSymbolDefinition => this;

    public ITypeSymbolWithMembers parent => containingType;

    /// <summary>
    /// The type of symbol this is (see <see cref="SymbolKind" />).
    /// </summary>
    public abstract SymbolKind kind { get; }

    /// <summary>
    /// If the symbol is "static", i.e. declared with the static modifier.
    /// </summary>
    public abstract bool isStatic { get; }

    /// <summary>
    /// If the symbol is "virtual", i.e. is defined but can be overridden
    /// </summary>
    public abstract bool isVirtual { get; }

    /// <summary>
    /// If the symbol is "abstract", i.e. must be overridden or cannot be constructed directly.
    /// </summary>
    public abstract bool isAbstract { get; }

    /// <summary>
    /// If the symbol is "override", i.e. overriding a virtual or abstract symbol.
    /// </summary>
    public abstract bool isOverride { get; }

    /// <summary>
    /// If the symbol is "sealed", i.e. cannot have child classes.
    /// </summary>
    public abstract bool isSealed { get; }

    public override string ToString() {
        return SymbolDisplay.DisplaySymbol(this).ToString();
    }

    internal void SetContainingType(NamedTypeSymbol symbol) {
        containingType = symbol;
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
            return left is not null;

        return (object)left != (object)right && !right.Equals(left);
    }
}
