using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound member access expression, bound from a <see cref="Syntax.MemberAccessExpressionSyntax" />.
/// </summary>
internal sealed class BoundMemberAccessExpression : BoundExpression {
    internal BoundMemberAccessExpression(BoundExpression operand, FieldSymbol member, bool isNullConditional) {
        this.operand = operand;
        this.member = member;
        this.isNullConditional = isNullConditional;
    }

    internal BoundExpression operand { get; }

    internal FieldSymbol member { get; }

    internal bool isNullConditional { get; }

    internal override BoundNodeKind kind => BoundNodeKind.MemberAccessExpression;

    internal override BoundType type => BoundType.Copy(member.type, isConstantReference: false, isReference: true);
}
