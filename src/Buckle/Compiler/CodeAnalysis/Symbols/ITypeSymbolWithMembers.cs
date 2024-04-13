using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// A type that contains members; all non-primitive types.
/// </summary>
public interface ITypeSymbolWithMembers : ITypeSymbol {
    /// <summary>
    /// Gets all members in their public representations.
    /// </summary>
    public ImmutableArray<ISymbol> GetMembers();
}
