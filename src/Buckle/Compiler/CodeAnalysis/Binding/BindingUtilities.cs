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
    internal static VariableSymbol GetAssignedVariableSymbol(BoundExpression expression) {
        if (expression is BoundVariableExpression v)
            return v.variable;
        if (expression is BoundMemberAccessExpression m)
            return GetAssignedVariableSymbol(m.right);
        if (expression is BoundIndexExpression i)
            return GetAssignedVariableSymbol(i.expression);

        throw ExceptionUtilities.Unreachable();
    }
}
