using Buckle;

namespace CommandLine;

public static partial class BuckleCommandLine {
    private readonly struct RunInfo {
        internal readonly BuildMode buildMode;
        internal readonly OutputKind outputKind;
        internal readonly string[] arguments;
        internal readonly string path;

        internal RunInfo(BuildMode buildMode, OutputKind outputKind, string[] arguments, string path) {
            this.buildMode = buildMode;
            this.outputKind = outputKind;
            this.arguments = arguments;
            this.path = path;
        }
    }
}
