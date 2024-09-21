using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound literal expression, bound from a <see cref="Syntax.LiteralExpressionSyntax" />.
/// </summary>
internal sealed class BoundLiteralExpression : BoundExpression {
    internal BoundLiteralExpression(object value) {
        type = TypeSymbol.Assume(value);
        constantValue = new ConstantValue(value);
    }

    /// <param name="type">Forces a <see cref="TypeSymbol" /> on a value instead of implying.</param>
    internal BoundLiteralExpression(object value, TypeSymbol type) {
        this.type = type;
        constantValue = new ConstantValue(value);
    }

    internal BoundLiteralExpression(ConstantValue constant, TypeSymbol type) {
        this.type = type;
        constantValue = constant;
    }

    internal override BoundNodeKind kind => BoundNodeKind.LiteralExpression;

    internal override TypeSymbol type { get; }

    internal override ConstantValue constantValue { get; }

    internal object value => constantValue.value;
}
