using Diagnostics;

namespace Belte.Diagnostics;

internal static class Error {
    private static DiagnosticInfo ErrorInfo(DiagnosticCode code) {
        return new DiagnosticInfo((int)code, "CL", DiagnosticType.Error);
    }

    private static DiagnosticInfo FatalErrorInfo(DiagnosticCode code) {
        return new DiagnosticInfo((int)code, "CL", DiagnosticType.Fatal);
    }

    internal static Diagnostic MissingFilenameO() {
        var message = "missing filename after '-o'";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_MissingFilenameO), message);
    }

    internal static Diagnostic MultipleExplains() {
        var message = "'--explain' specified more than once";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_MultipleExplains), message);
    }

    internal static Diagnostic MissingCodeExplain() {
        var message = "missing diagnostic code after '--explain'";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_MissingCodeExplain), message);
    }

    internal static Diagnostic MissingModuleName(string arg) {
        var message = $"missing name after '{arg}' (usage: '--modulename=<name>')";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_MissingModuleName), message);
    }

    internal static Diagnostic MissingReference(string arg) {
        var message = $"missing name after '{arg}' (usage: '--ref=<name>')";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_MissingReference), message);
    }

    internal static Diagnostic MissingEntrySymbol(string arg) {
        var message = $"missing symbol after '{arg}' (usage: '--entry=<symbol>')";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_MissingEntrySymbol), message);
    }

    internal static Diagnostic NoOptionAfterW() {
        var message = "must specify option after '-W' (usage: '-W<options>')";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_NoOptionAfterW), message);
    }

    internal static Diagnostic InvalidErrorCode(string error) {
        var message = $"'{error}' is not a valid error code; must be in the format: [BU|CL|RE]<code>" +
            "\n\texamples: BU0001, CL0001, BU54, CL012, RE0001, RE6";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_InvalidErrorCode), message);
    }

    internal static Diagnostic UnrecognizedWOption(string wArg) {
        var message = $"unrecognized option '{wArg}'";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_UnrecognizedWOption), message);
    }

    internal static Diagnostic UnusedErrorCode(string error) {
        var message = $"'{error}' is not a used error code";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_UnusedErrorCode), message);
    }

    internal static Diagnostic UnrecognizedOption(string arg) {
        var message = $"unrecognized command line option '{arg}'";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_UnrecognizedOption), message);
    }

    internal static Diagnostic CannotSpecifyWithDotnet() {
        var message = "cannot specify '-p', '-s', or '-c' with .NET integration";
        return new Diagnostic(FatalErrorInfo(DiagnosticCode.ERR_CannotSpecifyWithDotnet), message);
    }

    internal static Diagnostic CannotSpecifyWithMultipleFiles() {
        var message = "cannot specify output file with '-p', '-s', or '-c' with multiple files";
        return new Diagnostic(FatalErrorInfo(DiagnosticCode.ERR_CannotSpecifyWithMultipleFiles), message);
    }

    internal static Diagnostic CannotSpecifyWithInterpreter() {
        var message = "cannot specify output path or use '-p', '-s', or '-c' with interpreter";
        return new Diagnostic(FatalErrorInfo(DiagnosticCode.ERR_CannotSpecifyWithInterpreter), message);
    }

    internal static Diagnostic CannotSpecifyModuleNameWithoutDotnet() {
        var message = "cannot specify module name without .NET integration";
        return new Diagnostic(FatalErrorInfo(DiagnosticCode.ERR_CannotSpecifyModuleNameWithoutDotnet), message);
    }

    internal static Diagnostic CannotSpecifyReferencesWithoutDotnet() {
        var message = "cannot specify references without .NET integration";
        return new Diagnostic(FatalErrorInfo(DiagnosticCode.ERR_CannotSpecifyReferencesWithoutDotnet), message);
    }

    internal static Diagnostic NoInputFiles() {
        var message = "no input files";
        return new Diagnostic(FatalErrorInfo(DiagnosticCode.ERR_NoInputFiles), message);
    }

    internal static Diagnostic NoSuchFileOrDirectory(string name) {
        var message = $"{name}: no such file or directory";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_NoSuchFileOrDirectory), message);
    }
}
