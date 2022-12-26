
namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound if statement, bound from a <see cref="IfStatementSyntax" />.
/// </summary>
internal sealed class BoundIfStatement : BoundStatement {
    internal BoundIfStatement(BoundExpression condition, BoundStatement then, BoundStatement elseStatement) {
        this.condition = condition;
        this.then = then;
        this.elseStatement = elseStatement;
    }

    internal BoundExpression condition { get; }

    internal BoundStatement then { get; }

    internal BoundStatement elseStatement { get; }

    internal override BoundNodeKind kind => BoundNodeKind.IfStatement;
}
