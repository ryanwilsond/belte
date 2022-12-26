
namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound return statement, bound from a <see cref="ReturnStatementSyntax" />.
/// </summary>
internal sealed class BoundReturnStatement : BoundStatement {
    internal BoundReturnStatement(BoundExpression expression) {
        this.expression = expression;
    }

    internal BoundExpression expression { get; }

    internal override BoundNodeKind kind => BoundNodeKind.ReturnStatement;
}
