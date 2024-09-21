
namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// Extensions on the <see cref="CastKind" /> enum.
/// </summary>
internal static class CastKindExtensions {
    internal static bool IsImplicitCast(this CastKind self) {
        switch (self) {
            case CastKind.Identity:
            case CastKind.Implicit:
            case CastKind.ImplicitNullable:
            case CastKind.ImplicitReference:
            case CastKind.Boxing:
            case CastKind.BoxingImplicitNullable:
            case CastKind.AnyBoxing:
            case CastKind.AnyBoxingImplicitNullable:
                return true;
            default:
                return false;
        }
    }

    internal static bool IsNullableCast(this CastKind self) {
        switch (self) {
            case CastKind.ImplicitNullable:
            case CastKind.BoxingImplicitNullable:
            case CastKind.AnyBoxingImplicitNullable:
            case CastKind.ExplicitNullable:
            case CastKind.BoxingExplicitNullable:
            case CastKind.UnboxingImplicitNullable:
            case CastKind.UnboxingExplicitNullable:
            case CastKind.AnyBoxingExplicitNullable:
            case CastKind.AnyUnboxingImplicitNullable:
            case CastKind.AnyUnboxingExplicitNullable:
                return true;
            default:
                return false;
        }
    }

    internal static bool IsBoxingCast(this CastKind self) {
        switch (self) {
            case CastKind.Boxing:
            case CastKind.BoxingImplicitNullable:
            case CastKind.BoxingExplicitNullable:
            case CastKind.AnyBoxing:
            case CastKind.AnyBoxingImplicitNullable:
            case CastKind.AnyBoxingExplicitNullable:
                return true;
            default:
                return false;
        }
    }

    internal static bool IsUnboxingCast(this CastKind self) {
        switch (self) {
            case CastKind.Unboxing:
            case CastKind.UnboxingImplicitNullable:
            case CastKind.UnboxingExplicitNullable:
            case CastKind.AnyUnboxing:
            case CastKind.AnyUnboxingImplicitNullable:
            case CastKind.AnyUnboxingExplicitNullable:
                return true;
            default:
                return false;
        }
    }
}
