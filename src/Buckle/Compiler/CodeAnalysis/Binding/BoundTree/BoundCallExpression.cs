using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound call expression, bound from a <see cref="Syntax.CallExpressionSyntax" />.
/// </summary>
internal sealed class BoundCallExpression : BoundExpression {
    internal BoundCallExpression(
        BoundExpression expression,
        SubstitutedMethodSymbol method,
        ImmutableArray<BoundExpression> arguments) {
        this.expression = expression;
        this.method = method;
        this.arguments = arguments;
    }

    internal BoundExpression expression { get; }

    internal SubstitutedMethodSymbol method { get; }

    internal ImmutableArray<BoundExpression> arguments { get; }

    internal override BoundNodeKind kind => BoundNodeKind.CallExpression;

    internal override TypeSymbol type => method.type;
}
