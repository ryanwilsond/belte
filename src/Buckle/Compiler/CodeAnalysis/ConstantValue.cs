using System.Runtime.CompilerServices;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis;

/// <summary>
/// Constant value at compile time.
/// </summary>
internal partial class ConstantValue {
    internal static ConstantValue Unset => ConstantValueNull.Uninitialized;

    private protected ConstantValue() { }

    internal ConstantValue(object value) {
        this.value = value;
        specialType = SpecialTypeExtensions.SpecialTypeFromLiteralValue(value);
    }

    internal ConstantValue(object value, SpecialType specialType) {
        this.value = value;
        this.specialType = specialType;
    }

    internal ConstantValue(object value, SpecialType specialType, BelteDiagnostic[] diagnostics) {
        this.value = value;
        this.specialType = specialType;
        this.diagnostics = diagnostics;
    }

    internal object value { get; }

    internal SpecialType specialType { get; }

    internal BelteDiagnostic[] diagnostics { get; }

    internal bool isDefaultValue
        => (value is long i && i == 0) ||
           (value is bool b && !b) ||
           (value is double d && d == 0) ||
           (value is string s && s == "");

    internal bool isOne
        => (value is long i && i == 1) ||
           (value is bool b && b) ||
           (value is double d && d == 1);

    internal static bool IsNull(ConstantValue constant) {
        return constant is not null && constant.value is null;
    }

    internal static bool IsNotNull(ConstantValue constant) {
        return constant is not null && constant.value is not null;
    }

    internal bool IsIntegralValueZeroOrOne(out bool isOne) {
        if (isDefaultValue) {
            isOne = false;
        } else if (this.isOne) {
            isOne = true;
        } else {
            isOne = default;
            return false;
        }

        return value is long || value is bool;
    }

    public override int GetHashCode() {
        return RuntimeHelpers.GetHashCode(this);
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

        return (object)left == right || right.Equals(left);
    }

    public static bool operator !=(ConstantValue left, ConstantValue right) {
        if (right is null)
            return left is not null;

        return (object)left != right && !right.Equals(left);
    }
}
