
namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// Extensions on the <see cref="ConversionKind" /> enum.
/// </summary>
internal static class ConversionKindExtensions {
    internal static bool IsImplicitCast(this ConversionKind self) {
        switch (self) {
            case ConversionKind.Identity:
            case ConversionKind.Implicit:
            case ConversionKind.NullLiteral:
            case ConversionKind.ImplicitNullable:
            case ConversionKind.ImplicitReference:
            case ConversionKind.ImplicitUserDefined:
            case ConversionKind.ImplicitPointerToVoid:
            case ConversionKind.AnyBoxing:
                return true;
            default:
                return false;
        }
    }

    internal static bool IsNullableCast(this ConversionKind self) {
        switch (self) {
            case ConversionKind.ImplicitNullable:
            case ConversionKind.ExplicitNullable:
                return true;
            default:
                return false;
        }
    }

    internal static bool IsBoxingCast(this ConversionKind self) {
        switch (self) {
            case ConversionKind.AnyBoxing:
                return true;
            default:
                return false;
        }
    }

    internal static bool IsUnboxingCast(this ConversionKind self) {
        switch (self) {
            case ConversionKind.AnyUnboxing:
                return true;
            default:
                return false;
        }
    }

    internal static bool IsUserDefinedConversion(this ConversionKind self) {
        switch (self) {
            case ConversionKind.ImplicitUserDefined:
            case ConversionKind.ExplicitUserDefined:
                return true;
            default:
                return false;
        }
    }
}
