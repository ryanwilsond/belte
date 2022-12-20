using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound initializer list expression, bound from a <see cref="InitializerListExpression" />.
/// </summary>
internal sealed class BoundInitializerListExpression : BoundExpression {
    internal BoundInitializerListExpression(
        ImmutableArray<BoundExpression> items, int dimensions, BoundTypeClause itemType) {
        this.items = items;
        this.dimensions = dimensions;
        this.itemType = itemType;
    }

    internal ImmutableArray<BoundExpression> items { get; }

    internal int dimensions { get; }

    internal BoundTypeClause itemType { get; }

    internal override BoundNodeType type => BoundNodeType.LiteralExpression;

    // Immutable design makes this required
    internal override BoundTypeClause typeClause => new BoundTypeClause(
        itemType.lType, itemType.isImplicit, itemType.isConstantReference,
        itemType.isReference, itemType.isConstant, true, itemType.isLiteral, dimensions);
}
