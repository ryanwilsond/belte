using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound index expression, bound from a <see cref="Syntax.IndexExpressionSyntax" />.
/// </summary>
internal sealed class BoundIndexExpression : BoundExpression {
    internal BoundIndexExpression(
        BoundExpression expression,
        BoundExpression index,
        TypeSymbol type,
        bool isNullConditional) {
        this.expression = expression;
        this.index = index;
        this.isNullConditional = isNullConditional;
        this.type = type;
        constantValue = ConstantFolding.FoldIndex(this.expression, this.index);
    }

    internal BoundExpression expression { get; }

    internal BoundExpression index { get; }

    internal bool isNullConditional { get; }

    internal override BoundNodeKind kind => BoundNodeKind.IndexExpression;

    internal override ConstantValue constantValue { get; }

    internal override TypeSymbol type { get; }
}
