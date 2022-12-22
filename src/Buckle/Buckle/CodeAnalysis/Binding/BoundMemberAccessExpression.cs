using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound member access expression, bound from a <see cref="MemberAccessExpression" />.
/// </summary>
internal sealed class BoundMemberAccessExpression : BoundExpression {
    internal BoundMemberAccessExpression(BoundExpression operand, FieldSymbol member) {
        this.operand = operand;
        this.member = member;
    }

    internal BoundExpression operand { get; }

    internal FieldSymbol member { get; }

    internal override BoundNodeType type => BoundNodeType.MemberAccessExpression;

    internal override BoundTypeClause typeClause => BoundTypeClause.Reference(member.typeClause);
}
