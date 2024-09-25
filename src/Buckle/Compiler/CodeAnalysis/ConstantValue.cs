
namespace Buckle.CodeAnalysis;

/// <summary>
/// Constant value at compile time.
/// </summary>
internal sealed class ConstantValue {
    internal ConstantValue(object value) {
        this.value = value;
    }

    internal object value { get; }

    internal static bool IsNull(ConstantValue constant) {
        if (constant != null && constant.value is null)
            return true;

        return false;
    }

    internal static bool IsNotNull(ConstantValue constant) {
        if (constant != null && constant.value != null)
            return true;

        return false;
    }

    public override int GetHashCode() {
        return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);
    }

    public override bool Equals(object obj) {
        return Equals(obj as ConstantValue);
    }

    public bool Equals(ConstantValue other) {
        if (other is null)
            return false;

        return value == other.value;
    }

    public static bool operator ==(ConstantValue left, ConstantValue right) {
        if (right is null)
            return left is null;

        return (object)left == (object)right || right.Equals(left);
    }

    public static bool operator !=(ConstantValue left, ConstantValue right) {
        if (right is null)
            return left is not null;

        return (object)left != (object)right && !right.Equals(left);
    }
}