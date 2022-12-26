using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound call expression, bound from a <see cref="CallExpressionSyntax" />.
/// </summary>
internal sealed class BoundCallExpression : BoundExpression {
    internal BoundCallExpression(FunctionSymbol function, ImmutableArray<BoundExpression> arguments) {
        this.function = function;
        this.arguments = arguments;
    }

    internal FunctionSymbol function { get; }

    internal ImmutableArray<BoundExpression> arguments { get; }

    internal override BoundNodeKind kind => BoundNodeKind.CallExpression;

    internal override BoundType type => function?.type;
}
