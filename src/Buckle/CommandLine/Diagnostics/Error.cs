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
    internal static Diagnostic MissingFilenameO() {
        var message = "missing filename after '-o'";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_MissingFilenameO), message);
    }

    internal static Diagnostic MultipleExplains() {
        var message = "cannot specify '--explain' more than once";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_MultipleExplains), message);
    }

    internal static Diagnostic MissingCodeExplain() {
        var message = "missing diagnostic code after '--explain' (usage: '--explain[BU|RE|CL]<code>')";
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

    internal static Diagnostic UnableToOpenFile(string fileName) {
        var message = $"failed to open file '{fileName}'; most likely due to the file being used by another process";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_UnableToOpenFile), message);
    }

    internal static Diagnostic MissingSeverity(string arg) {
        var message = $"missing severity after '{arg}' (usage: '--severity=<severity>')";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_MissingSeverity), message);
    }

    internal static Diagnostic UnrecognizedSeverity(string severity) {
        var message = $"unrecognized severity '{severity}'";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_UnrecognizedSeverity), message);
    }

    internal static Diagnostic UnrecognizedOption(string arg) {
        var message = $"unrecognized command line option '{arg}'; see 'buckle --help'";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_UnrecognizedOption), message);
    }

    internal static Diagnostic NoSuchFileOrDirectory(string name) {
        var message = $"{name}: no such file or directory";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_NoSuchFileOrDirectory), message);
    }

    internal static Diagnostic InvalidErrorCode(string error) {
        var message = $"'{error}' is not a valid diagnostic code; must be in the format: [BU|CL|RE]<code>" +
            $"{Environment.NewLine}\texamples: BU0001, CL0001, BU54, CL012, RE0001, RE6";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_InvalidErrorCode), message);
    }

    internal static Diagnostic UnusedErrorCode(string error) {
        var message = $"'{error}' is not a used diagnostic code";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_UnusedErrorCode), message);
    }

    internal static Diagnostic MissingWarningLevel(string arg) {
        var message = $"missing warning level after '{arg}' (usage: '--warnlevel=<warning level>')";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_MissingWarningLevel), message);
    }

    internal static Diagnostic InvalidWarningLevel(string warningLevel) {
        var message = $"invalid warning level '{warningLevel}'; warning level must be a number between 0 and 2";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_InvalidWarningLevel), message);
    }

    internal static Diagnostic MissingWIgnoreCode(string arg) {
        var message = $"missing warning code after '{arg}' (usage: '--wignore=<[BU|RE|CL]<code>,...>')";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_MissingWIgnoreCode), message);
    }

    internal static Diagnostic MissingWIncludeCode(string arg) {
        var message = $"missing warning code after '{arg}' (usage: '--winclude=<[BU|RE|CL]<code>,...>')";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_MissingWIncludeCode), message);
    }

    internal static Diagnostic CodeIsNotWarning(string code) {
        var message = $"'{code}' is not the code of a warning";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_CodeIsNotWarning), message);
    }

    internal static Diagnostic MissingType(string arg) {
        var message = $"missing project type after '{arg}' (usage: '--type=[console|graphics|...]')";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_MissingType), message);
    }

    internal static Diagnostic UnrecognizedType(string type) {
        var message = $"unrecognized project type '{type}'";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_UnrecognizedType), message);
    }

    internal static Diagnostic MissingVerbosePath(string arg) {
        var message = $"missing path after '{arg}' (usage: '--verbose-path=<path>')";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_MissingVerbosePath), message);
    }

    internal static Diagnostic MissingMaxCoreCount(string arg) {
        var message = $"missing core count after '{arg}' (usage: '-m:<count>')";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_MissingMaxCoreCount), message);
    }

    internal static Diagnostic InvalidMaxCoreCount(string arg) {
        var message = $"'{arg}' is not a valid core count; core count must be a positive integer";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_InvalidMaxCoreCount), message);
    }

    internal static Diagnostic MissingEntryName(string arg) {
        var message = $"missing type name after '{arg}' (usage: '--entry=<name>')";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_MissingEntryName), message);
    }

    private static DiagnosticInfo ErrorInfo(DiagnosticCode code) {
        return new DiagnosticInfo((int)code, "CL", DiagnosticSeverity.Error);
    }
}
