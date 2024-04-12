
namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound member access expression, bound from a <see cref="Syntax.MemberAccessExpressionSyntax" />.
/// </summary>
internal sealed class BoundMemberAccessExpression : BoundExpression {
    internal BoundMemberAccessExpression(
        BoundExpression expression,
        BoundExpression member,
        BoundType type,
        bool isNullConditional,
        bool isStaticAccess) {
        this.expression = expression;
        this.member = member;
        this.isNullConditional = isNullConditional;
        this.type = type;
        this.isStaticAccess = isStaticAccess;
    }

    internal override BoundNodeKind kind => BoundNodeKind.MemberAccessExpression;

    internal override BoundType type { get; }

    internal override BoundConstant constantValue => member.constantValue;

    internal BoundExpression expression { get; }

    internal BoundExpression member { get; }

    internal bool isNullConditional { get; }

    internal bool isStaticAccess { get; }
}
