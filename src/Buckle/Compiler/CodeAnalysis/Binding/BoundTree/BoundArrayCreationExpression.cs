using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound array creation expression, bound from a <see cref="Syntax.ObjectCreationExpressionSyntax" />.
/// </summary>
internal sealed class BoundArrayCreationExpression : BoundExpression {
    internal BoundArrayCreationExpression(TypeSymbol type, ImmutableArray<BoundExpression> sizes) {
        this.type = type;
        this.sizes = sizes;
    }

    internal override BoundNodeKind kind => BoundNodeKind.ArrayCreationExpression;

    internal override TypeSymbol type { get; }

    internal ImmutableArray<BoundExpression> sizes { get; }
}
