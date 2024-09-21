
namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// A field symbol. This is a variable declared as a member of a type.
/// </summary>
internal sealed class FieldSymbol : VariableSymbol {
    /// <summary>
    /// Creates a <see cref="FieldSymbol" />.
    /// </summary>
    internal FieldSymbol(
        string name,
        TypeWithAnnotations type,
        ConstantValue constant,
        DeclarationModifiers modifiers,
        Accessibility accessibility)
        : base(name, type, constant, modifiers, accessibility) {
    }

    public override SymbolKind kind => SymbolKind.Field;
}
