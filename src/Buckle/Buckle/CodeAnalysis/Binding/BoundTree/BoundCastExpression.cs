
namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound cast expression, bound from a <see cref="Syntax.CastExpressionSyntax" />.
/// In addition, a <see cref="BoundCastExpression" /> can be produced from a <see cref="Syntax.CallExpressionSyntax" />
/// using a type name as the method name.<br/>
/// E.g.
/// <code>
/// int(3.4)
/// </code>
/// </summary>
internal sealed class BoundCastExpression : BoundExpression {
    internal BoundCastExpression(BoundType type, BoundExpression expression) {
        this.type = type;
        this.expression = expression;
        constantValue = ConstantFolding.FoldCast(this.expression, this.type);
    }

    internal BoundExpression expression { get; }

    internal override BoundNodeKind kind => BoundNodeKind.CastExpression;

    internal override BoundConstant constantValue { get; }

    internal override BoundType type { get; }
}
