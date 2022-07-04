using System;
using Diagnostics;

namespace Belte.Diagnostics;

internal static class Warning {
    private static DiagnosticInfo WarningInfo(DiagnosticCode code) {
        return new DiagnosticInfo((int)code, DiagnosticType.Warning);
    }

    public static Diagnostic ReplInvokeIgnore() {
        var message = "all arguments are ignored when invoking the repl";
        return new Diagnostic(WarningInfo(DiagnosticCode.WRN_ReplInvokeIgnore), message);
    }

    public static Diagnostic IgnoringUnknownFileType(string filename) {
        var message = $"unknown file type of input file '{filename}'; ignoring";
        return new Diagnostic(WarningInfo(DiagnosticCode.WRN_IgnoringUnknownFileType), message);
    }

    public static Diagnostic IgnoringCompiledFile(string filename) {
        var message = $"{filename}: file already compiled; ignoring";
        return new Diagnostic(WarningInfo(DiagnosticCode.WRN_IgnoringCompiledFile), message);
    }
}
