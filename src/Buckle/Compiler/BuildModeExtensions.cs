
namespace Buckle;

internal static class BuildModeExtensions {
    internal static bool Evaluating(this BuildMode buildMode) {
        return buildMode is BuildMode.Repl or BuildMode.Evaluate or BuildMode.Independent;
    }
}
