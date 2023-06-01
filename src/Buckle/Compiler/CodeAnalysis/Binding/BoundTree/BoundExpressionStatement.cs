
namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound expression statement, bound from a <see cref="Syntax.ExpressionStatementSyntax" />.
/// </summary>
internal sealed class BoundExpressionStatement : BoundStatement {
    internal BoundExpressionStatement(BoundExpression expression) {
        this.expression = expression;
    }

    internal BoundExpression expression { get; }

    internal override BoundNodeKind kind => BoundNodeKind.ExpressionStatement;
}
