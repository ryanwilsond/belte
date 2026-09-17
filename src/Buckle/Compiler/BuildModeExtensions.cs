
namespace Buckle;

public static class BuildModeExtensions {
    internal static bool Evaluating(this BuildMode buildMode) {
        return buildMode is BuildMode.Repl or BuildMode.Evaluate;
    }

    internal static bool Emitting(this BuildMode buildMode) {
        return buildMode is BuildMode.Execute or BuildMode.Dotnet;
    }

    internal static bool SupportsNonTypeTemplates(this BuildMode buildMode) {
        return buildMode.Evaluating();
    }

    internal static bool SupportsNonIntegralEnums(this BuildMode buildMode) {
        return buildMode.Evaluating();
    }

    public static bool RunsImmediately(this BuildMode buildMode) {
        switch (buildMode) {
            case BuildMode.AutoRun:
            case BuildMode.Interpret:
            case BuildMode.Evaluate:
            case BuildMode.Execute:
            case BuildMode.Emulate:
                return true;
            default:
                return false;
        }
    }

    public static bool SupportsDotnetReferences(this BuildMode buildMode) {
        switch (buildMode) {
            case BuildMode.AutoRun:
            case BuildMode.Execute:
            case BuildMode.Dotnet:
            case BuildMode.CSharpTranspile:
                return true;
            default:
                return false;
        }
    }
}
