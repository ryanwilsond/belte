
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

    public override int GetHashCode() {
        return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);
    }

    public override bool Equals(object obj) {
        return Equals(obj as BoundConstant);
    }

    public bool Equals(BoundConstant other) {
        if (other is null)
            return false;

        return value == other.value;
    }

    public static bool operator ==(BoundConstant left, BoundConstant right) {
        if (right is null)
            return left is null;

        return (object)left == (object)right || right.Equals(left);
    }

    public static bool operator !=(BoundConstant left, BoundConstant right) {
        if (right is null)
            return left is not null;

        return (object)left != (object)right && !right.Equals(left);
    }
}
