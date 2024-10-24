using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound empty expression, bound from a <see cref="Syntax.EmptyExpressionSyntax" />.
/// Converted to NOP statements eventually.
/// </summary>
internal sealed class BoundEmptyExpression : BoundExpression {
    internal BoundEmptyExpression() { }

    internal override BoundNodeKind kind => BoundNodeKind.EmptyExpression;

    internal override TypeSymbol type => null;
}
