using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound initializer dictionary expression, bound from a <see cref="Syntax.InitializerDictionaryExpressionSyntax" />.
/// </summary>
internal sealed class BoundInitializerDictionaryExpression : BoundExpression {
    internal BoundInitializerDictionaryExpression(
        ImmutableArray<(BoundExpression, BoundExpression)> items,
        BoundType type) {
        this.items = items;
        this.type = type;
    }

    internal ImmutableArray<(BoundExpression, BoundExpression)> items { get; }

    internal override BoundNodeKind kind => BoundNodeKind.InitializerDictionaryExpression;

    internal override BoundType type { get; }

    internal override BoundConstant constantValue { get; }
}
