using Diagnostics;

namespace Belte.Diagnostics;

/// <summary>
/// All predefined error messages that can be used by the command line.
/// The return value for all methods is a new diagnostic that needs to be manually handled or added to a
/// <see cref="DiagnosticQueue<T>" />.
/// The parameters for all methods allow the error messages to be more dynamic and represent the error more accurately.
/// </summary>
internal static class Error {
    /// <summary>
    /// CL0001. Run `buckle --explain CL0001` on the command line for more info.
    /// </summary>
    internal static Diagnostic MissingFilenameO() {
        var message = "missing filename after '-o'";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_MissingFilenameO), message);
    }

    /// <summary>
    /// CL0002. Run `buckle --explain CL0002` on the command line for more info.
    /// </summary>
    internal static Diagnostic MultipleExplains() {
        var message = "'--explain' specified more than once";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_MultipleExplains), message);
    }

    /// <summary>
    /// CL0003. Run `buckle --explain CL0003` on the command line for more info.
    /// </summary>
    internal static Diagnostic MissingCodeExplain() {
        var message = "missing diagnostic code after '--explain'";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_MissingCodeExplain), message);
    }

    /// <summary>
    /// CL0004. Run `buckle --explain CL0004` on the command line for more info.
    /// </summary>
    internal static Diagnostic MissingModuleName(string arg) {
        var message = $"missing name after '{arg}' (usage: '--modulename=<name>')";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_MissingModuleName), message);
    }

    /// <summary>
    /// CL0005. Run `buckle --explain CL0005` on the command line for more info.
    /// </summary>
    internal static Diagnostic MissingReference(string arg) {
        var message = $"missing name after '{arg}' (usage: '--ref=<name>')";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_MissingReference), message);
    }

    /// <summary>
    /// CL0006. Run `buckle --explain CL0006` on the command line for more info.
    /// </summary>
    internal static Diagnostic MissingEntrySymbol(string arg) {
        var message = $"missing symbol after '{arg}' (usage: '--entry=<symbol>')";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_MissingEntrySymbol), message);
    }

    /// <summary>
    /// CL0007. Run `buckle --explain CL0007` on the command line for more info.
    /// </summary>
    internal static Diagnostic NoOptionAfterW() {
        var message = "must specify option after '-W' (usage: '-W<options>')";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_NoOptionAfterW), message);
    }

    /// <summary>
    /// CL0008. Run `buckle --explain CL0008` on the command line for more info.
    /// </summary>
    internal static Diagnostic UnrecognizedWOption(string wArg) {
        var message = $"unrecognized option '{wArg}'";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_UnrecognizedWOption), message);
    }

    /// <summary>
    /// CL0009. Run `buckle --explain CL0009` on the command line for more info.
    /// </summary>
    internal static Diagnostic UnrecognizedOption(string arg) {
        var message = $"unrecognized command line option '{arg}'";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_UnrecognizedOption), message);
    }

    /// <summary>
    /// CL0011. Run `buckle --explain CL0011` on the command line for more info.
    /// </summary>
    internal static Diagnostic CannotSpecifyWithDotnet() {
        var message = "cannot specify '-p', '-s', or '-c' with .NET integration";
        return new Diagnostic(FatalErrorInfo(DiagnosticCode.ERR_CannotSpecifyWithDotnet), message);
    }

    /// <summary>
    /// CL0012. Run `buckle --explain CL0012` on the command line for more info.
    /// </summary>
    internal static Diagnostic CannotSpecifyWithMultipleFiles() {
        var message = "cannot specify output file with '-p', '-s', or '-c' with multiple files";
        return new Diagnostic(FatalErrorInfo(DiagnosticCode.ERR_CannotSpecifyWithMultipleFiles), message);
    }

    /// <summary>
    /// CL0013. Run `buckle --explain CL0013` on the command line for more info.
    /// </summary>
    internal static Diagnostic CannotSpecifyWithInterpreter() {
        var message = "cannot specify output path or use '-p', '-s', or '-c' with interpreter";
        return new Diagnostic(FatalErrorInfo(DiagnosticCode.ERR_CannotSpecifyWithInterpreter), message);
    }

    /// <summary>
    /// CL0014. Run `buckle --explain CL0014` on the command line for more info.
    /// </summary>
    internal static Diagnostic CannotSpecifyModuleNameWithoutDotnet() {
        var message = "cannot specify module name without .NET integration";
        return new Diagnostic(FatalErrorInfo(DiagnosticCode.ERR_CannotSpecifyModuleNameWithoutDotnet), message);
    }

    /// <summary>
    /// CL0015. Run `buckle --explain CL0015` on the command line for more info.
    /// </summary>
    internal static Diagnostic CannotSpecifyReferencesWithoutDotnet() {
        var message = "cannot specify references without .NET integration";
        return new Diagnostic(FatalErrorInfo(DiagnosticCode.ERR_CannotSpecifyReferencesWithoutDotnet), message);
    }

    /// <summary>
    /// CL0016. Run `buckle --explain CL0016` on the command line for more info.
    /// </summary>
    internal static Diagnostic NoInputFiles() {
        var message = "no input files";
        return new Diagnostic(FatalErrorInfo(DiagnosticCode.ERR_NoInputFiles), message);
    }

    /// <summary>
    /// CL0017. Run `buckle --explain CL0017` on the command line for more info.
    /// </summary>
    internal static Diagnostic NoSuchFileOrDirectory(string name) {
        var message = $"{name}: no such file or directory";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_NoSuchFileOrDirectory), message);
    }

    /// <summary>
    /// CL00019. Run `buckle --explain CL00019` on the command line for more info.
    /// </summary>
    internal static Diagnostic InvalidErrorCode(string error) {
        var message = $"'{error}' is not a valid error code; must be in the format: [BU|CL|RE]<code>" +
            "\n\texamples: BU0001, CL0001, BU54, CL012, RE0001, RE6";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_InvalidErrorCode), message);
    }

    /// <summary>
    /// CL0021. Run `buckle --explain CL0021` on the command line for more info.
    /// </summary>
    internal static Diagnostic UnusedErrorCode(string error) {
        var message = $"'{error}' is not a used error code";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_UnusedErrorCode), message);
    }

    private static DiagnosticInfo ErrorInfo(DiagnosticCode code) {
        return new DiagnosticInfo((int)code, "CL", DiagnosticType.Error);
    }

    private static DiagnosticInfo FatalErrorInfo(DiagnosticCode code) {
        return new DiagnosticInfo((int)code, "CL", DiagnosticType.Fatal);
    }
}
