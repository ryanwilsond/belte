using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound conditional access expression, bound from a <see cref="Syntax.MemberAccessExpressionSyntax" />.
/// </summary>
internal sealed class BoundConditionalAccessExpression : BoundExpression {
    internal BoundConditionalAccessExpression(BoundExpression accessExpression, TypeSymbol type) {
        this.accessExpression = accessExpression;
        this.type = type;
    }

    internal override BoundNodeKind kind => BoundNodeKind.ConditionalAccessExpression;

    internal override TypeSymbol type { get; }

    internal override ConstantValue constantValue => accessExpression.constantValue;

    internal BoundExpression accessExpression { get; }
}
