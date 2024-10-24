using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// A local variable symbol. This is a variable declared anywhere except the global scope (thus being more local).
/// </summary>
internal abstract class LocalSymbol : DataContainerSymbol {
    /// <summary>
    /// Creates a <see cref="LocalSymbol" />.
    /// </summary>
    /// <param name="name">Name of the variable.</param>
    /// <param name="type"><see cref="BoundType" /> of the variable.</param>
    /// <param name="constant"><see cref="ConstantValue" /> of the variable.</param>
    internal LocalSymbol(
        string name,
        TypeWithAnnotations type,
        ConstantValue constant,
        DeclarationModifiers modifiers)
        : base(name, type, constant, modifiers, Accessibility.NotApplicable) { }

    public override SymbolKind kind => SymbolKind.Local;

    internal abstract SyntaxToken identifier { get; }
}
