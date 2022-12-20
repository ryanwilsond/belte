using System.Collections.Immutable;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// A function symbol.
/// </summary>
internal sealed class StructSymbol : TypeSymbol {
    /// <summary>
    /// Creates a <see cref="StructSymbol" />.
    /// </summary>
    /// <param name="name">Name of struct.</param>
    /// <param name="symbols">Symbols contained in the struct.</param>
    /// <param name="declaration">Declaration of the struct.</param>
    internal StructSymbol(
        string name, ImmutableArray<Symbol> symbols, StructDeclaration declaration = null)
        : base(name) {
        this.symbols = symbols;
        this.declaration = declaration;
    }

    /// <summary>
    /// All contained symbols.
    /// </summary>
    internal ImmutableArray<Symbol> symbols { get; }

    /// <summary>
    /// Declaration of struct (see <see cref="StructDeclaration">).
    /// </summary>
    internal StructDeclaration declaration { get; }

    internal override SymbolType type => SymbolType.Type;
}
