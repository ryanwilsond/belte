using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// A type that contains members; all non-primitive types.
/// </summary>
internal interface ITypeSymbolWithMembers : ITypeSymbol {
    /// <summary>
    /// All symbols contained within the type and it's scope.
    /// </summary>
    public ImmutableArray<Symbol> members { get; }

    /// <summary>
    /// Gets all members.
    /// </summary>
    public ImmutableArray<Symbol> GetMembers();

    /// <summary>
    /// Gets all members with the given name.
    /// </summary>
    public ImmutableArray<Symbol> GetMembers(string name);
}
