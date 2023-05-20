using System.Collections.Immutable;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// A class symbol.
/// </summary>
internal sealed class ClassSymbol : TypeSymbol, ITypeSymbolWithMembers {
    /// <summary>
    /// Creates a <see cref="ClassSymbol" />.
    /// </summary>
    /// <param name="name">Name of the class.</param>
    /// <param name="symbols">Symbols contained in the class.</param>
    /// <param name="declaration">Declaration of the class.</param>
    internal ClassSymbol(
        string name, ImmutableArray<TemplateParameterSymbol> templateParameters,
        ImmutableArray<Symbol> symbols, ClassDeclarationSyntax declaration = null)
        : base(name) {
        this.symbols = symbols;
        this.declaration = declaration;
        this.templateParameters = templateParameters;
    }

    public override SymbolKind kind => SymbolKind.Type;

    public ImmutableArray<Symbol> symbols { get; }

    public ImmutableArray<TemplateParameterSymbol> templateParameters { get; }

    internal override int arity => templateParameters.Length;

    /// <summary>
    /// Declaration of the class (see <see cref="ClassDeclarationSyntax">).
    /// </summary>
    internal ClassDeclarationSyntax declaration { get; }
}
