using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound member access expression, bound from a <see cref="Syntax.MemberAccessExpressionSyntax" />.
/// </summary>
internal sealed class BoundMemberAccessExpression : BoundExpression {
    internal BoundMemberAccessExpression(
        BoundExpression operand,
        Symbol member,
        BoundType type,
        bool isNullConditional) {
        this.operand = operand;
        this.member = member;
        this.isNullConditional = isNullConditional;
        this.type = type;
    }

    internal override BoundNodeKind kind => BoundNodeKind.MemberAccessExpression;

    // internal override BoundType type => BoundType.CopyWith(member.type, isConstantReference: false, isReference: true);
    internal override BoundType type { get; }

    internal BoundExpression operand { get; }

    internal Symbol member { get; }

    internal bool isNullConditional { get; }
}
