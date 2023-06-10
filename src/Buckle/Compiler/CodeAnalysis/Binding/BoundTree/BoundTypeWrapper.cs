
namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound type wrapper expression. Bound from a <see cref="Syntax.TypeExpressionSyntax" />.
/// Purely an implementation detail. Used to wrap constant expressions without a full type, or to wrap template
/// arguments that are types.
/// </summary>
internal sealed class BoundTypeWrapper : BoundExpression {
    internal BoundTypeWrapper(BoundType type, BoundConstant constant) {
        this.type = type;
        this.constantValue = constant;
    }

    internal override BoundNodeKind kind => BoundNodeKind.TypeWrapper;

    internal override BoundConstant constantValue { get; }

    internal override BoundType type { get; }
}
