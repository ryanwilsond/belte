
namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound while statement, bound from a parser WhileStatement.
/// </summary>
internal sealed class BoundWhileStatement : BoundLoopStatement {
    internal BoundWhileStatement(
        BoundExpression condition, BoundStatement body, BoundLabel breakLabel, BoundLabel continueLabel)
        : base(breakLabel, continueLabel) {
        this.condition = condition;
        this.body = body;
    }

    internal BoundExpression condition { get; }

    internal BoundStatement body { get; }

    internal override BoundNodeType type => BoundNodeType.WhileStatement;
}
