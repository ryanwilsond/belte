
namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound return statement, bound from a <see cref="ReturnStatement" />.
/// </summary>
internal sealed class BoundReturnStatement : BoundStatement {
    internal BoundReturnStatement(BoundExpression expression) {
        this.expression = expression;
    }

    internal BoundExpression expression { get; }

    internal override BoundNodeType type => BoundNodeType.ReturnStatement;
}
