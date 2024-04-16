using Buckle.CodeAnalysis.Binding;

namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// A field symbol. This is a variable declared as a member of a type.
/// </summary>
internal sealed class FieldSymbol : VariableSymbol {
    /// <summary>
    /// Creates a <see cref="FieldSymbol" />.
    /// </summary>
    /// <param name="name">Name of the field.</param>
    /// <param name="type"><see cref="BoundType" /> of the field.</param>
    /// <param name="constant"><see cref="BoundConstant" /> of the field.</param>
    internal FieldSymbol(
        string name,
        BoundType type,
        BoundConstant constant,
        DeclarationModifiers modifiers = DeclarationModifiers.None)
        : base(name, type, constant, modifiers) {
    }

    public override SymbolKind kind => SymbolKind.Field;
}
