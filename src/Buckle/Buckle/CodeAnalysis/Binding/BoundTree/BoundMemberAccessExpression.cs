using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound member access expression, bound from a <see cref="MemberAccessExpressionSyntax" />.
/// </summary>
internal sealed class BoundMemberAccessExpression : BoundExpression {
    internal BoundMemberAccessExpression(BoundExpression operand, FieldSymbol member) {
        this.operand = operand;
        this.member = member;
    }

    internal BoundExpression operand { get; }

    internal FieldSymbol member { get; }

    internal override BoundNodeKind kind => BoundNodeKind.MemberAccessExpression;

    internal override BoundType type => BoundType.Reference(member.type);
}
