using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class FieldSymbolWithModifiers : FieldSymbol {
    private protected SymbolCompletionState _state;

    private protected abstract DeclarationModifiers _modifiers { get; }

    internal abstract TextLocation errorLocation { get; }

    internal sealed override bool HasComplete(CompletionParts part) => _state.HasComplete(part);

    internal sealed override bool isStatic => (_modifiers & DeclarationModifiers.Static) != 0;

    internal sealed override bool isConst => (_modifiers & DeclarationModifiers.Const) != 0;

    internal sealed override bool isConstExpr => (_modifiers & DeclarationModifiers.ConstExpr) != 0;

    internal sealed override Accessibility declaredAccessibility => ModifierHelpers.EffectiveAccessibility(_modifiers);
}
