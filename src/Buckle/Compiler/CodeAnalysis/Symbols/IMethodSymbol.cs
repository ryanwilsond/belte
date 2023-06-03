
namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// Represents a method.
/// </summary>
public interface IMethodSymbol : ISymbol {
    /// <summary>
    /// Gets the signature of this without the return type or parameter names.
    /// </summary>
    /// <returns>Signature if this <see cref="MethodSymbol" />.</returns>
    public string Signature();
}
