
namespace Buckle;

internal static class BuildModeExtensions {
    internal static bool Evaluating(this BuildMode buildMode) {
        return buildMode is BuildMode.Repl or BuildMode.Evaluate;
    }

    internal static bool Emitting(this BuildMode buildMode) {
        return buildMode is BuildMode.Execute or BuildMode.Dotnet;
    }

    internal static bool PermitsNonTypeTemplates(this BuildMode buildMode) {
        return buildMode.Evaluating();
    }
}
