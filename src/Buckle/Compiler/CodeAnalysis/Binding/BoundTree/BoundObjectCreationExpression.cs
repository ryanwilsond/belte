using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound object creation expression, bound from a <see cref="Syntax.ObjectCreationExpressionSyntax" />.
/// </summary>
internal sealed class BoundObjectCreationExpression : BoundExpression {
    internal BoundObjectCreationExpression(BoundType type, ImmutableArray<BoundExpression> arguments) {
        this.type = type;
        this.arguments = arguments;
    }

    internal override BoundNodeKind kind => BoundNodeKind.ObjectCreationExpression;

    internal override BoundType type { get; }

    internal ImmutableArray<BoundExpression> arguments { get; }
}
