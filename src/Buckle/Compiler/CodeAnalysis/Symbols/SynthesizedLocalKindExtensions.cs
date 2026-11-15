
namespace Buckle.CodeAnalysis.Symbols;

internal static class SynthesizedLocalKindExtensions {
    internal static bool IsLongLived(this SynthesizedLocalKind kind) {
        return kind >= SynthesizedLocalKind.UserDefined;
    }

    internal static bool IsSlotReusable(this SynthesizedLocalKind kind, bool isDebug) {
        if (isDebug)
            return !IsLongLived(kind);

        switch (kind) {
            case SynthesizedLocalKind.UserDefined:
            case SynthesizedLocalKind.LambdaDisplayClass:
                return false;
            default:
                return true;
        }
    }
}
