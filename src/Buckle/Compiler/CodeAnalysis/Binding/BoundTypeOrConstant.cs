
namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// Template argument value.
/// </summary>
internal sealed class BoundTypeOrConstant {
    internal BoundTypeOrConstant(BoundConstant constant) {
        this.constant = constant;
        type = null;
    }

    internal BoundTypeOrConstant(BoundType type) {
        constant = null;
        this.type = type;
    }

    internal bool isConstant { get; }

    internal BoundConstant constant { get; }

    internal BoundType type { get; }
}
