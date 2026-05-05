using Diagnostics;

namespace Belte.Diagnostics;

internal static class Warning {
    internal static Diagnostic UnableToCopyFile(string source, string destination) {
        var message = $"unable to copy '{source}' to '{destination}'; most likely due to a file being used by another process";
        return new Diagnostic(WarningInfo(DiagnosticCode.WRN_UnableToCopyFile), message);
    }

    private static DiagnosticInfo WarningInfo(DiagnosticCode code) {
        return new DiagnosticInfo((int)code, "CL", DiagnosticSeverity.Warning);
    }
}
