
namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound error expression, signally that binding a expression failed.
/// Using at temporary error expression allows catching of future unrelated issues before quitting.
/// Also prevents cascading errors, as if a error expression is seen the Binder ignores it as it knows the error
/// Was already reported.
/// </summary>
internal sealed class BoundErrorExpression : BoundExpression {
    internal BoundErrorExpression() { }

    internal override BoundNodeType type => BoundNodeType.ErrorExpression;

    internal override BoundTypeClause typeClause => new BoundTypeClause(null);
}
