using Buckle.CodeAnalysis.Symbols;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound literal expression, bound from a <see cref="LiteralExpressionSyntax" />.
/// </summary>
internal sealed class BoundLiteralExpression : BoundExpression {
    internal BoundLiteralExpression(object value, bool isArtificial = false) {
        if (value is bool)
            type = new BoundType(TypeSymbol.Bool, isLiteral: true);
        else if (value is int)
            type = new BoundType(TypeSymbol.Int, isLiteral: true);
        else if (value is string)
            type = new BoundType(TypeSymbol.String, isLiteral: true);
        else if (value is double)
            type = new BoundType(TypeSymbol.Decimal, isLiteral: true);
        else if (value == null)
            type = new BoundType(null, isLiteral: true, isNullable: true);
        else
            throw new BelteInternalException(
                $"BoundLiteralExpression: unexpected literal '{value}' of type '{value.GetType()}'");

        this.isArtificial = isArtificial;
        constantValue = new BoundConstant(value);
    }

    /// <param name="override">Forces a <see cref="BoundType" /> on a value instead of implying.</param>
    internal BoundLiteralExpression(object value, BoundType @override) {
        type = BoundType.Copy(@override, isLiteral: true);
        constantValue = new BoundConstant(value);
    }

    internal override BoundNodeKind kind => BoundNodeKind.LiteralExpression;

    internal override BoundType type { get; }

    internal override BoundConstant constantValue { get; }

    internal object value => constantValue.value;

    internal bool isArtificial { get; }
}
