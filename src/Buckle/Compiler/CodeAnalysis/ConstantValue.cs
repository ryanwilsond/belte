using System.Runtime.CompilerServices;
using Buckle.CodeAnalysis.CodeGeneration;
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

    internal ConstantValue(object value, SpecialType specialType) : this(value, specialType, null) { }

    internal ConstantValue(object value, SpecialType specialType, BelteDiagnostic[] diagnostics) {
        this.value = value;
        this.specialType = specialType;
        this.diagnostics = diagnostics;

#if DEBUG
        if (value is not null) {
            if (specialType is SpecialType.Nullable or SpecialType.None)
                throw ExceptionUtilities.UnexpectedValue(specialType);

            var inferredSpecialType = CodeGenerator.NormalizeNumericType(
                SpecialTypeExtensions.SpecialTypeFromLiteralValue(value)
            );

            var targetType = CodeGenerator.NormalizeNumericType(specialType);

            if (inferredSpecialType != targetType)
                throw ExceptionUtilities.UnexpectedValue(specialType);
        }
#endif
    }

    internal virtual object value { get; }

    internal virtual SpecialType specialType { get; }

    internal virtual BelteDiagnostic[] diagnostics { get; }

    internal bool isDefaultValue
        => LiteralUtilities.TypeHasConstantDefaultValue(specialType) &&
            LiteralUtilities.GetDefaultValue(specialType).Equals(value);

    internal bool isOne
        => (value is long l && l == 1) ||
           (value is ulong ul && ul == 1) ||
           (value is int i && i == 1) ||
           (value is uint u && u == 1) ||
           (value is short s && s == 1) ||
           (value is ushort w && w == 1) ||
           (value is sbyte sb && sb == 1) ||
           (value is byte by && by == 1) ||
           (value is bool b && b) ||
           (value is double d && d == 1) ||
           (value is float f && f == 1);

    internal static bool IsNull(ConstantValue constant) {
        if (constant is TemplateConstantValue)
            return false;

        return constant is not null && constant.value is null;
    }

    internal static bool IsString(ConstantValue constant) {
        if (constant is TemplateConstantValue)
            return false;

        return constant.specialType == SpecialType.String && constant.value is string;
    }

    internal static bool IsNotNull(ConstantValue constant) {
        if (constant is TemplateConstantValue)
            return false;

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

        return value is long or ulong or int or uint or short or ushort or byte or sbyte or bool;
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
        return value?.GetHashCode() ?? RuntimeHelpers.GetHashCode(this);
    }

    public override bool Equals(object obj) {
        return Equals(obj as ConstantValue);
    }

    public virtual bool Equals(ConstantValue other) {
        if (other is null)
            return false;

        if (value is null)
            return other.value is null;

        return value.Equals(other.value);
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
