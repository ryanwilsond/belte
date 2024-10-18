
namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// Represents a local or global.
/// </summary>
public interface IDataContainerSymbol : ISymbol {
    /// <summary>
    /// The type.
    /// </summary>
    public ITypeSymbol typeSymbol { get; }
}
