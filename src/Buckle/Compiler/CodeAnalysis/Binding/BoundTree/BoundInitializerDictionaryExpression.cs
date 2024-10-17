using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound initializer dictionary expression, bound from a <see cref="Syntax.InitializerDictionaryExpressionSyntax" />.
/// </summary>
internal sealed class BoundInitializerDictionaryExpression : BoundExpression {
    internal BoundInitializerDictionaryExpression(
        ImmutableArray<(BoundExpression, BoundExpression)> items,
        TypeSymbol type) {
        this.items = items;
        this.type = type;
    }

    internal ImmutableArray<(BoundExpression, BoundExpression)> items { get; }

    internal override BoundNodeKind kind => BoundNodeKind.InitializerDictionaryExpression;

    internal override TypeSymbol type { get; }

    internal override ConstantValue constantValue { get; }
}
