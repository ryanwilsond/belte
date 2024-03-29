using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound initializer list expression, bound from a <see cref="Syntax.InitializerListExpressionSyntax" />.
/// </summary>
internal sealed class BoundInitializerListExpression : BoundExpression {
    internal BoundInitializerListExpression(ImmutableArray<BoundExpression> items, BoundType type) {
        this.items = items;
        this.type = type;
        constantValue = ConstantFolding.FoldInitializerList(this.items);
    }

    internal BoundInitializerListExpression(BoundConstant constantValue, BoundType type) {
        this.type = type;
        this.constantValue = constantValue;
    }

    internal ImmutableArray<BoundExpression> items { get; }

    internal override BoundNodeKind kind => BoundNodeKind.LiteralExpression;

    internal override BoundType type { get; }

    internal override BoundConstant constantValue { get; }
}
