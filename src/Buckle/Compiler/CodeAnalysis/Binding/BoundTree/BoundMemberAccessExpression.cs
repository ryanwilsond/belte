
namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound member access expression, bound from a <see cref="Syntax.MemberAccessExpressionSyntax" />.
/// </summary>
internal sealed class BoundMemberAccessExpression : BoundExpression {
    internal BoundMemberAccessExpression(
        BoundExpression left,
        BoundExpression right,
        bool isNullConditional,
        bool isStaticAccess) {
        this.left = left;
        this.right = right;
        this.isNullConditional = isNullConditional;
        this.isStaticAccess = isStaticAccess;
    }

    internal override BoundNodeKind kind => BoundNodeKind.MemberAccessExpression;

    internal override BoundType type => right.type;

    internal override BoundConstant constantValue => right.constantValue;

    internal BoundExpression left { get; }

    internal BoundExpression right { get; }

    internal bool isNullConditional { get; }

    internal bool isStaticAccess { get; }
}
