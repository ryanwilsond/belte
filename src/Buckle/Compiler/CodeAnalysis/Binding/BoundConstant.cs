
namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// Bound constant.
/// </summary>
internal sealed class BoundConstant {
    internal BoundConstant(object value) {
        this.value = value;
    }

    internal object value { get; }

    internal static bool IsNull(BoundConstant constant) {
        if (constant != null && constant.value is null)
            return true;

        return false;
    }

    internal static bool IsNotNull(BoundConstant constant) {
        if (constant != null && constant.value != null)
            return true;

        return false;
    }
}
