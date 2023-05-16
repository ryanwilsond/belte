using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// A type that contains members; all non-primitive types.
/// </summary>
internal interface ITypeSymbolWithMembers : ITypeSymbol {
    /// <summary>
    /// All symbols contained within the type and it's scope.
    /// </summary>
    internal ImmutableArray<Symbol> symbols { get; }
}
