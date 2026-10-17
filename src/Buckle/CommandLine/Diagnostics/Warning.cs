using Diagnostics;

namespace Belte.Diagnostics;

internal static class Warning {
    internal static Diagnostic UnableToCopyFile(string source, string destination) {
        var message = $"unable to copy '{source}' to '{destination}'; most likely due to a file being used by another process";
        return new Diagnostic(WarningInfo(DiagnosticCode.WRN_UnableToCopyFile), message);
    }

    internal static Diagnostic R2RFailed() {
        var message = $"failed to create R2R assembly";
        return new Diagnostic(WarningInfo(DiagnosticCode.WRN_R2RFailed), message);
    }

    private static DiagnosticInfo WarningInfo(DiagnosticCode code) {
        return new DiagnosticInfo((int)code, "CL", DiagnosticSeverity.Warning);
    }
}
