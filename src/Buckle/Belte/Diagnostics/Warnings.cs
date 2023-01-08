using Diagnostics;

namespace Belte.Diagnostics;

/// <summary>
/// All predefined warning messages that can be used by the command line.
/// The return value for all methods is a new diagnostic that needs to be manually handled or added to a
/// <see cref="DiagnosticQueue<T>" />.
/// The parameters for all methods allow the warning messages to be more dynamic and represent the
/// warning more accurately.
/// </summary>
internal static class Warning {
    /// <summary>
    /// CL0010. Run `buckle --explain CL0010` on the command line for more info.
    /// </summary>
    internal static Diagnostic ReplInvokeIgnore() {
        var message = "all arguments are ignored when invoking the repl";
        return new Diagnostic(WarningInfo(DiagnosticCode.WRN_ReplInvokeIgnore), message);
    }

    /// <summary>
    /// CL0018. Run `buckle --explain CL0018` on the command line for more info.
    /// </summary>
    internal static Diagnostic IgnoringUnknownFileType(string filename) {
        var message = $"unknown file type of input file '{filename}'; ignoring";
        return new Diagnostic(WarningInfo(DiagnosticCode.WRN_IgnoringUnknownFileType), message);
    }

    /// <summary>
    /// CL0020. Run `buckle --explain CL0020` on the command line for more info.
    /// </summary>
    internal static Diagnostic IgnoringCompiledFile(string filename) {
        var message = $"{filename}: file already compiled; ignoring";
        return new Diagnostic(WarningInfo(DiagnosticCode.WRN_IgnoringCompiledFile), message);
    }

    /// <summary>
    /// CL0022. Run `buckle --explain CL0022` on the command line for more info.
    /// </summary>
    internal static Diagnostic CorruptInstallation() {
        var message =
            $"installation is corrupt; all compiler features are enabled except the `--explain` and `--help` options";
        return new Diagnostic(WarningInfo(DiagnosticCode.WRN_CorruptInstallation), message);
    }

    private static DiagnosticInfo WarningInfo(DiagnosticCode code) {
        return new DiagnosticInfo((int)code, "CL", DiagnosticType.Warning);
    }
}
