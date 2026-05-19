
namespace Buckle.CodeAnalysis.Binding;

internal static class BoundNodeExtensions {
    internal static bool HasErrors(this BoundNode node) {
        return node is not null && node.hasErrors;
    }

    internal static bool IsConstructorInitializer(this BoundStatement statement) {
        if (statement.kind == BoundKind.ExpressionStatement) {
            var expression = ((BoundExpressionStatement)statement).expression;

            return expression.kind == BoundKind.CallExpression &&
                ((BoundCallExpression)expression).IsConstructorInitializer();
        }

        return false;
    }

    internal static bool IsDefaultValue(this BoundExpression node) {
        if (node.kind == BoundKind.DefaultExpression || node.kind == BoundKind.DefaultLiteral)
            return true;

        var constValue = node.constantValue;

        if (constValue is not null)
            return constValue == LiteralUtilities.TryGetDefaultValue(node.type);

        return false;
    }
}
