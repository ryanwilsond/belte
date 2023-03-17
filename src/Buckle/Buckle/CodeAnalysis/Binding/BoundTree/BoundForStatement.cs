
namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound for statement, bound from a <see cref="Syntax.ForStatementSyntax" />.
/// </summary>
internal sealed class BoundForStatement : BoundLoopStatement {
    internal BoundForStatement(
        BoundStatement initializer, BoundExpression condition, BoundExpression step,
        BoundStatement body, BoundLabel breakLabel, BoundLabel continueLabel)
        : base(breakLabel, continueLabel) {
        this.initializer = initializer;
        this.condition = condition;
        this.step = step;
        this.body = body;
    }

    internal BoundStatement initializer { get; }

    internal BoundExpression condition { get; }

    internal BoundExpression step { get; }

    internal BoundStatement body { get; }

    internal override BoundNodeKind kind => BoundNodeKind.ForStatement;
}
