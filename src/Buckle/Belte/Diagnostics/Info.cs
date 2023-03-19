using Diagnostics;

namespace Belte.Diagnostics;

/// <summary>
/// All predefined info messages that can be used by the command line.
/// The return value for all methods is a new diagnostic that needs to be manually handled or added to a
/// <see cref="DiagnosticQueue<T>" />.
/// The parameters for all methods allow the info messages to be more dynamic and represent the info more accurately.
/// </summary>
internal static class Info {
    /// <summary>
    /// CL0010. Run `buckle --explain CL0010` on the command line for more info.
    /// </summary>
    internal static Diagnostic ReplInvokeIgnore() {
        var message = "all arguments are ignored when invoking the repl";
        return new Diagnostic(InfoInfo(DiagnosticCode.INF_ReplInvokeIgnore), message);
    }

    /// <summary>
    /// CL0018. Run `buckle --explain CL0018` on the command line for more info.
    /// </summary>
    internal static Diagnostic IgnoringUnknownFileType(string filename) {
        var message = $"unknown file type of input file '{filename}'; ignoring";
        return new Diagnostic(InfoInfo(DiagnosticCode.INF_IgnoringUnknownFileType), message);
    }

    /// <summary>
    /// CL0020. Run `buckle --explain CL0020` on the command line for more info.
    /// </summary>
    internal static Diagnostic IgnoringCompiledFile(string filename) {
        var message = $"{filename}: file already compiled; ignoring";
        return new Diagnostic(InfoInfo(DiagnosticCode.INF_IgnoringCompiledFile), message);
    }

    private static DiagnosticInfo InfoInfo(DiagnosticCode code) {
        return new DiagnosticInfo((int)code, "CL", DiagnosticSeverity.Info);
    }
}
