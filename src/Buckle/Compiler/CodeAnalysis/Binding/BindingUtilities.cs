using Buckle.CodeAnalysis.Symbols;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// Utilities used during the binding step.
/// </summary>
internal static class BindingUtilities {
    /// <summary>
    /// Gets the root-most <see cref="VariableSymbol"/> from an assignment right hand.
    /// </summary>
    internal static LocalSymbol GetAssignedVariableSymbol(BoundExpression expression) {
        if (expression is BoundVariableExpression v)
            return v.variable;
        if (expression is BoundFieldAccessExpression f)
            return f.field;
        if (expression is BoundIndexExpression i)
            return GetAssignedVariableSymbol(i.expression);

        throw ExceptionUtilities.Unreachable();
    }
}
