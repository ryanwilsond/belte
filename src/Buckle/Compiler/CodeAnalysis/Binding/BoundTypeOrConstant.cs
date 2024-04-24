
namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// Template argument value.
/// </summary>
internal sealed class BoundTypeOrConstant {
    internal BoundTypeOrConstant(BoundConstant constant, BoundType type) {
        this.constant = constant;
        this.type = type;
        isConstant = true;
    }

    internal BoundTypeOrConstant(BoundType type) {
        constant = null;
        isConstant = false;
        this.type = type;
    }

    internal bool isConstant { get; }

    internal BoundConstant constant { get; }

    internal BoundType type { get; }
}
