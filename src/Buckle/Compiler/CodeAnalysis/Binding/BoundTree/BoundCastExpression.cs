using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound cast expression, bound from a <see cref="Syntax.CastExpressionSyntax" />.
/// </summary>
internal sealed class BoundCastExpression : BoundExpression {
    internal BoundCastExpression(TypeSymbol type, BoundExpression operand) {
        this.type = type;
        this.operand = operand;
        constantValue = ConstantFolding.FoldCast(this.operand, this.type);
    }

    internal BoundExpression operand { get; }

    internal override BoundNodeKind kind => BoundNodeKind.CastExpression;

    internal override ConstantValue constantValue { get; }

    internal override TypeSymbol type { get; }
}
