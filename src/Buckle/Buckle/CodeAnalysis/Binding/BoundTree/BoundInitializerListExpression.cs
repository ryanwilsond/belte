using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound initializer list expression, bound from a <see cref="InitializerListExpressionSyntax" />.
/// </summary>
internal sealed class BoundInitializerListExpression : BoundExpression {
    internal BoundInitializerListExpression(
        ImmutableArray<BoundExpression> items, BoundType type) {
        this.items = items;
        this.type = type;
        constantValue = ConstantFolding.FoldInitializerList(this.items);
    }

    internal ImmutableArray<BoundExpression> items { get; }

    internal override BoundNodeKind kind => BoundNodeKind.LiteralExpression;

    internal override BoundType type { get; }

    internal override BoundConstant constantValue { get; }
}
