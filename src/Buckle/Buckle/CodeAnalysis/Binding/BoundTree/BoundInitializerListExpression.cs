using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound initializer list expression, bound from a <see cref="InitializerListExpressionSyntax" />.
/// </summary>
internal sealed class BoundInitializerListExpression : BoundExpression {
    internal BoundInitializerListExpression(
        ImmutableArray<BoundExpression> items, int dimensions, BoundType itemType) {
        this.items = items;
        this.dimensions = dimensions;
        this.itemType = itemType;
    }

    internal ImmutableArray<BoundExpression> items { get; }

    internal int dimensions { get; }

    internal BoundType itemType { get; }

    internal override BoundNodeKind kind => BoundNodeKind.LiteralExpression;

    internal override BoundType type => itemType == null
        ? null
        : BoundType.Copy(itemType, isNullable: true, dimensions: dimensions);
}
