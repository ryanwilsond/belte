using System.Runtime.CompilerServices;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Diagnostics;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis;

/// <summary>
/// Constant value at compile time.
/// </summary>
internal partial class ConstantValue {
    internal static ConstantValue Unset => ConstantValueNull.Uninitialized;
    internal static ConstantValue Null => new ConstantValue(null, SpecialType.None);

    private protected ConstantValue() { }

    internal ConstantValue(object value, SpecialType specialType) {
        this.value = value;
        this.specialType = specialType;

        if (value is not null && specialType == SpecialType.Nullable)
            throw ExceptionUtilities.UnexpectedValue(specialType);
    }

    internal ConstantValue(object value, SpecialType specialType, BelteDiagnostic[] diagnostics) {
        this.value = value;
        this.specialType = specialType;
        this.diagnostics = diagnostics;

        if (value is not null && specialType == SpecialType.Nullable)
            throw ExceptionUtilities.UnexpectedValue(specialType);
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

    internal bool IsNegativeNumeric() {
        switch (specialType) {
            case SpecialType.Int8:
                return (sbyte)value < 0;
            case SpecialType.Int16:
                return (short)value < 0;
            case SpecialType.Int32:
                return (int)value < 0;
            case SpecialType.Int64:
            case SpecialType.Int:
                return (long)value < 0;
            case SpecialType.Float32:
                return (float)value < 0;
            case SpecialType.Float64:
            case SpecialType.Decimal:
                return (double)value < 0;
            default:
                return false;
        }
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
