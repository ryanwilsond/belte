
namespace Buckle.CodeAnalysis.Symbols;

internal static class SynthesizedLocalKindExtensions {
    internal static bool IsLongLived(this SynthesizedLocalKind kind) {
        return kind >= SynthesizedLocalKind.UserDefined;
    }
}
