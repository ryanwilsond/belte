using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound call expression, bound from a <see cref="Syntax.CallExpressionSyntax" />.
/// </summary>
internal sealed class BoundCallExpression : BoundExpression {
    internal BoundCallExpression(
        BoundExpression operand,
        MethodSymbol method,
        ImmutableArray<BoundExpression> arguments) {
        this.operand = operand;
        this.method = method;
        this.arguments = arguments;
    }

    internal BoundExpression operand { get; }

    internal MethodSymbol method { get; }

    internal ImmutableArray<BoundExpression> arguments { get; }

    internal override BoundNodeKind kind => BoundNodeKind.CallExpression;

    internal override BoundType type => method.type;
}
