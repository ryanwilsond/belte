using System.Collections.Immutable;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// A struct symbol.
/// </summary>
internal sealed class StructSymbol : TypeSymbol, ITypeSymbolWithMembers {
    /// <summary>
    /// Creates a <see cref="StructSymbol" />.
    /// </summary>
    /// <param name="name">Name of the struct.</param>
    /// <param name="symbols">Symbols contained in the struct.</param>
    /// <param name="declaration">Declaration of the struct.</param>
    internal StructSymbol(
        string name, ImmutableArray<Symbol> symbols, StructDeclarationSyntax declaration = null)
        : base(name) {
        this.symbols = symbols;
        this.declaration = declaration;
    }

    public ImmutableArray<Symbol> symbols { get; }

    public override SymbolKind kind => SymbolKind.Type;

    /// <summary>
    /// Declaration of the struct (see <see cref="StructDeclarationSyntax">).
    /// </summary>
    internal StructDeclarationSyntax declaration { get; }
}
