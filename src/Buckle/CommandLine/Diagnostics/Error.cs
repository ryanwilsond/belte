using System;
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
        var message = "cannot specify '--explain' more than once";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_MultipleExplains), message);
    }

    /// <summary>
    /// CL0003. Run `buckle --explain CL0003` on the command line for more info.
    /// </summary>
    internal static Diagnostic MissingCodeExplain() {
        var message = "missing diagnostic code after '--explain' (usage: '--explain[BU|RE|CL]<code>')";
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
    internal static Diagnostic UnableToOpenFile(string fileName) {
        var message = $"failed to open file '{fileName}'; most likely due to the file being used by another process";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_UnableToOpenFile), message);
    }

    /// <summary>
    /// CL0007. Run `buckle --explain CL0007` on the command line for more info.
    /// </summary>
    internal static Diagnostic MissingSeverity(string arg) {
        var message = $"missing severity after '{arg}' (usage: '--severity=<severity>')";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_MissingSeverity), message);
    }

    /// <summary>
    /// CL0008. Run `buckle --explain CL0008` on the command line for more info.
    /// </summary>
    internal static Diagnostic UnrecognizedSeverity(string severity) {
        var message = $"unrecognized severity '{severity}'";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_UnrecognizedSeverity), message);
    }

    /// <summary>
    /// CL0009. Run `buckle --explain CL0009` on the command line for more info.
    /// </summary>
    internal static Diagnostic UnrecognizedOption(string arg) {
        var message = $"unrecognized command line option '{arg}'; see 'buckle --help'";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_UnrecognizedOption), message);
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
        var message = $"'{error}' is not a valid diagnostic code; must be in the format: [BU|CL|RE]<code>" +
            $"{Environment.NewLine}\texamples: BU0001, CL0001, BU54, CL012, RE0001, RE6";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_InvalidErrorCode), message);
    }

    /// <summary>
    /// CL0021. Run `buckle --explain CL0021` on the command line for more info.
    /// </summary>
    internal static Diagnostic UnusedErrorCode(string error) {
        var message = $"'{error}' is not a used diagnostic code";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_UnusedErrorCode), message);
    }

    /// <summary>
    /// CL0024. Run `buckle --explain CL0024` on the command line for more info.
    /// </summary>
    internal static Diagnostic MissingWarningLevel(string arg) {
        var message = $"missing warning level after '{arg}' (usage: '--warnlevel=<warning level>')";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_MissingWarningLevel), message);
    }

    /// <summary>
    /// CL0025. Run `buckle --explain CL0025` on the command line for more info.
    /// </summary>
    internal static Diagnostic InvalidWarningLevel(string warningLevel) {
        var message = $"invalid warning level '{warningLevel}'; warning level must be a number between 0 and 2";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_InvalidWarningLevel), message);
    }

    /// <summary>
    /// CL0026. Run `buckle --explain CL0026` on the command line for more info.
    /// </summary>
    internal static Diagnostic MissingWIgnoreCode(string arg) {
        var message = $"missing warning code after '{arg}' (usage: '--wignore=<[BU|RE|CL]<code>,...>')";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_MissingWIgnoreCode), message);
    }

    /// <summary>
    /// CL0027. Run `buckle --explain CL0027` on the command line for more info.
    /// </summary>
    internal static Diagnostic MissingWIncludeCode(string arg) {
        var message = $"missing warning code after '{arg}' (usage: '--winclude=<[BU|RE|CL]<code>,...>')";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_MissingWIncludeCode), message);
    }

    /// <summary>
    /// CL0028. Run `buckle --explain CL0028` on the command line for more info.
    /// </summary>
    internal static Diagnostic CodeIsNotWarning(string code) {
        var message = $"'{code}' is not the code of a warning";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_CodeIsNotWarning), message);
    }

    /// <summary>
    /// CL0029. Run `buckle --explain CL0029` on the command line for more info.
    /// </summary>
    internal static Diagnostic MissingType(string arg) {
        var message = $"missing project type after '{arg}' (usage: '--type=[console|graphics|...]')";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_MissingType), message);
    }

    /// <summary>
    /// CL0030. Run `buckle --explain CL0030` on the command line for more info.
    /// </summary>
    internal static Diagnostic UnrecognizedType(string type) {
        var message = $"unrecognized project type '{type}'";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_UnrecognizedType), message);
    }

    private static DiagnosticInfo ErrorInfo(DiagnosticCode code) {
        return new DiagnosticInfo((int)code, "CL", DiagnosticSeverity.Error);
    }
}
