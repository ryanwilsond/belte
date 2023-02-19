
namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound type wrapper expression. No <see cref="Parser" /> equivalent.
/// Purely an implementation detail, used to wrap constant expressions without a full type.
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
