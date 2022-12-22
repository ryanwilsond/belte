
namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound expression statement, bound from a <see cref="ExpressionStatement" />.
/// </summary>
internal sealed class BoundExpressionStatement : BoundStatement {
    internal BoundExpressionStatement(BoundExpression expression) {
        this.expression = expression;
    }

    internal BoundExpression expression { get; }

    internal override BoundNodeType type => BoundNodeType.ExpressionStatement;
}
