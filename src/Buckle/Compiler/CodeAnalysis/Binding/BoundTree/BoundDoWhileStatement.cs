
namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound do while statement, bound from a <see cref="Syntax.DoWhileStatementSyntax" />.
/// </summary>
internal sealed class BoundDoWhileStatement : BoundLoopStatement {
    internal BoundDoWhileStatement(
        BoundStatement body, BoundExpression condition, BoundLabel breakLabel, BoundLabel continueLabel)
        : base(breakLabel, continueLabel) {
        this.body = body;
        this.condition = condition;
    }

    internal BoundStatement body { get; }

    internal BoundExpression condition { get; }

    internal override BoundNodeKind kind => BoundNodeKind.DoWhileStatement;
}
