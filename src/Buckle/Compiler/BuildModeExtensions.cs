
namespace Buckle;

internal static class BuildModeExtensions {
    internal static bool Evaluating(this BuildMode buildMode) {
        return buildMode == BuildMode.Repl || buildMode == BuildMode.Evaluate;
    }
}
