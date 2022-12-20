using Buckle.CodeAnalysis.Binding;

namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// A local variable symbol. This is a variable declared anywhere except the global scope (thus being more local).
/// </summary>
internal class FieldSymbol : VariableSymbol {
    /// <summary>
    /// Creates a <see cref="FieldSymbol" />.
    /// </summary>
    /// <param name="name">Name of the field.</param>
    /// <param name="typeClause"><see cref="BoundTypeClause" /> of the field.</param>
    /// <param name="constant"><see cref="ConstantValue" /> of the field.</param>
    internal FieldSymbol(string name, BoundTypeClause typeClause, BoundConstant constant)
        : base(name, typeClause, constant) { }

    internal override SymbolType type => SymbolType.Field;
}
