using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound throw expression, bound from a <see cref="Syntax.ThrowExpressionSyntax" />.
/// </summary>
internal sealed class BoundThrowExpression : BoundExpression {
    internal BoundThrowExpression(BoundExpression exception) {
        this.exception = exception;
    }

    internal override BoundNodeKind kind => BoundNodeKind.ThrowExpression;

    internal override TypeSymbol type => exception.type;

    internal BoundExpression exception { get; }
}
