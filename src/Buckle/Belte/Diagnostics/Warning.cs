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
    /// CL0022. Run `buckle --explain CL0022` on the command line for more info.
    /// </summary>
    internal static Diagnostic CorruptInstallation() {
        var message =
            $"installation is corrupt; all compiler features are enabled except the `--explain` and `--help` options";
        return new Diagnostic(WarningInfo(DiagnosticCode.WRN_CorruptInstallation), message);
    }

    private static DiagnosticInfo WarningInfo(DiagnosticCode code) {
        return new DiagnosticInfo((int)code, "CL", DiagnosticSeverity.Warning);
    }
}
