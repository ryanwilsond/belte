using Buckle.CodeAnalysis.Symbols;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound literal expression, bound from a <see cref="Syntax.LiteralExpressionSyntax" />.
/// </summary>
internal sealed class BoundLiteralExpression : BoundExpression {
    internal BoundLiteralExpression(object value, bool isArtificial = false) {
        type = BoundType.Assume(value);
        this.isArtificial = isArtificial;
        constantValue = new BoundConstant(value);
    }

    /// <param name="override">Forces a <see cref="BoundType" /> on a value instead of implying.</param>
    internal BoundLiteralExpression(object value, BoundType @override) {
        type = BoundType.CopyWith(@override, isLiteral: true);
        constantValue = new BoundConstant(value);
    }

    internal override BoundNodeKind kind => BoundNodeKind.LiteralExpression;

    internal override BoundType type { get; }

    internal override BoundConstant constantValue { get; }

    internal object value => constantValue.value;

    internal bool isArtificial { get; }
}
