using Buckle.CodeAnalysis.Display;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// A base symbol.
/// </summary>
internal abstract class Symbol : ISymbol {
    /// <summary>
    /// Name of the symbol.
    /// </summary>
    public virtual string name => "";

    public ITypeSymbolWithMembers parent => containingType;

    /// <summary>
    /// The accessibility/protection level of the symbol.
    /// </summary> <summary>
    internal abstract Accessibility accessibility { get; }

    internal abstract Symbol containingSymbol { get; }

    /// <summary>
    /// The type that contains this symbol, or null if nothing is containing this symbol.
    /// </summary>
    internal virtual NamedTypeSymbol containingType {
        get {
            var containerAsType = containingSymbol as NamedTypeSymbol;

            if ((object)containerAsType == containingSymbol)
                return containerAsType;

            return containingSymbol.containingType;
        }
    }

    /// <summary>
    /// Gets the original definition of the symbol.
    /// </summary>
    internal Symbol originalDefinition => _originalSymbolDefinition;

    internal bool isDefinition => (object)this == originalDefinition;

    private protected virtual Symbol _originalSymbolDefinition => this;

    /// <summary>
    /// The type of symbol this is (see <see cref="SymbolKind" />).
    /// </summary>
    public abstract SymbolKind kind { get; }

    /// <summary>
    /// If the symbol is "static", i.e. declared with the static modifier.
    /// </summary>
    internal abstract bool isStatic { get; }

    /// <summary>
    /// If the symbol is "virtual", i.e. is defined but can be overridden
    /// </summary>
    internal abstract bool isVirtual { get; }

    /// <summary>
    /// If the symbol is "abstract", i.e. must be overridden or cannot be constructed directly.
    /// </summary>
    internal abstract bool isAbstract { get; }

    /// <summary>
    /// If the symbol is "override", i.e. overriding a virtual or abstract symbol.
    /// </summary>
    internal abstract bool isOverride { get; }

    /// <summary>
    /// If the symbol is "sealed", i.e. cannot have child classes.
    /// </summary>
    internal abstract bool isSealed { get; }

    internal virtual Compilation declaringCompilation {
        get {
            if (!isDefinition)
                return originalDefinition.declaringCompilation;

            return containingSymbol.declaringCompilation;
        }
    }

    // TODO Will need to change this to an immutable array when `partial` keyword is added
    internal abstract SyntaxReference syntaxReference { get; }

    internal virtual void AddDeclarationDiagnostics(BelteDiagnosticQueue diagnostics) {
        if (diagnostics.Count > 0)
            declaringCompilation.diagnostics.Move(diagnostics);
    }

    internal bool Equals(Symbol other) {
        return Equals(other, SymbolEqualityComparer.Default.compareKind);
    }

    internal bool Equals(Symbol other, SymbolEqualityComparer comparer) {
        return Equals(other, comparer.compareKind);
    }

    internal virtual bool Equals(Symbol other, TypeCompareKind compareKind) {
        return (object)this == other;
    }

    public override string ToString() {
        return SymbolDisplay.DisplaySymbol(this).ToString();
    }

    public override int GetHashCode() {
        return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);
    }

    public sealed override bool Equals(object obj) {
        return Equals(obj as Symbol, SymbolEqualityComparer.Default.compareKind);
    }

    public static bool operator ==(Symbol left, Symbol right) {
        if (right is null)
            return left is null;

        return (object)left == right || right.Equals(left);
    }

    public static bool operator !=(Symbol left, Symbol right) {
        if (right is null)
            return left is not null;

        return (object)left != right && !right.Equals(left);
    }
}
