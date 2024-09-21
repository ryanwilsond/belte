
namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// Represents a local or global variable in a method body.
/// </summary>
///
public interface IVariableSymbol : ISymbol {
    /// <summary>
    /// The type.
    /// </summary>
    public ITypeSymbol typeSymbol { get; }
}
