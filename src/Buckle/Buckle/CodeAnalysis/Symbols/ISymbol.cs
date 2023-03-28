
namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// Represents a symbol (class, method, parameter, etc.) exposed by the compiler.
/// </summary>
public interface ISymbol {
    /// <summary>
    /// Name of the symbol.
    /// </summary>
    public string name { get; }

    /// <summary>
    /// The type of symbol this is (see <see cref="SymbolKind" />).
    /// </summary>
    public abstract SymbolKind kind { get; }
}
