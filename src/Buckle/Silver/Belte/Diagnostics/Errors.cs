using System;
using Diagnostics;

namespace Belte.Diagnostics;

internal static class Error {
    private static DiagnosticInfo ErrorInfo(DiagnosticCode code) {
        return new DiagnosticInfo((int)code, DiagnosticType.Error);
    }

    private static DiagnosticInfo FatalErrorInfo(DiagnosticCode code) {
        return new DiagnosticInfo((int)code, DiagnosticType.Fatal);
    }

    public static Diagnostic UnknownReplCommand(string line) {
        var message = $"unknown repl command '{line}'";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_UnknownReplCommand), message);
    }

    public static Diagnostic WrongArgumentCount(string name, string parameterNames) {
        var message = $"invalid number of arguments\nusage: #{name} {parameterNames}";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_WrongArgumentCount), message);
    }

    public static Diagnostic UndefinedSymbol(string name) {
        var message = $"undefined symbol '{name}'";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_UndefinedSymbol), message);
    }

    public static Diagnostic MissingFilenameO() {
        var message = "missing filename after '-o'";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_MissingFilenameO), message);
    }

    public static Diagnostic MultipleExplains() {
        var message = "'--explain' specified more than once";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_MultipleExplains), message);
    }

    public static Diagnostic MissingCodeExplain() {
        var message = "missing diagnostic code after '--explain'";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_MissingCodeExplain), message);
    }

    public static Diagnostic MissingModuleName(string arg) {
        var message = $"missing name after '{arg}' (usage: '--modulename=<name>')";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_MissingModuleName), message);
    }

    public static Diagnostic MissingReference(string arg) {
        var message = $"missing name after '{arg}' (usage: '--ref=<name>')";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_MissingReference), message);
    }

    public static Diagnostic MissingEntrySymbol(string arg) {
        var message = $"missing symbol after '{arg}' (usage: '--entry=<symbol>')";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_MissingEntrySymbol), message);
    }

    public static Diagnostic NoOptionAfterW() {
        var message = "must specify option after '-W' (usage: '-W<options>')";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_NoOptionAfterW), message);
    }

    public static Diagnostic InvalidErrorCode(string errorString) {
        var message = $"'{errorString}' is not a valid error code";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_InvalidErrorCode), message);
    }

    public static Diagnostic UnrecognizedWOption(string wArg) {
        var message = $"unrecognized option '{wArg}'";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_UnrecognizedWOption), message);
    }

    public static Diagnostic UnrecognizedOption(string arg) {
        var message = $"unrecognized command line option '{arg}'";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_UnrecognizedOption), message);
    }

    public static Diagnostic CannotSpecifyWithDotnet() {
        var message = "cannot specify '-p', '-s', or '-c' with .NET integration";
        return new Diagnostic(FatalErrorInfo(DiagnosticCode.ERR_CannotSpecifyWithDotnet), message);
    }

    public static Diagnostic CannotSpecifyWithMultipleFiles() {
        var message = "cannot specify output file with '-p', '-s', or '-c' with multiple files";
        return new Diagnostic(FatalErrorInfo(DiagnosticCode.ERR_CannotSpecifyWithMultipleFiles), message);
    }

    public static Diagnostic CannotSpecifyWithInterpreter() {
        var message = "cannot specify output path or use '-p', '-s', or '-c' with interpreter";
        return new Diagnostic(FatalErrorInfo(DiagnosticCode.ERR_CannotSpecifyWithInterpreter), message);
    }

    public static Diagnostic CannotSpecifyModuleNameWithDotnet() {
        var message = "cannot specify module name without .NET integration";
        return new Diagnostic(FatalErrorInfo(DiagnosticCode.ERR_CannotSpecifyModuleNameWithDotnet), message);
    }

    public static Diagnostic CannotSpecifyReferencesWithDotnet() {
        var message = "cannot specify references without .NET integration";
        return new Diagnostic(FatalErrorInfo(DiagnosticCode.ERR_CannotSpecifyReferencesWithDotnet), message);
    }

    public static Diagnostic NoInputFiles() {
        var message = "no input files";
        return new Diagnostic(FatalErrorInfo(DiagnosticCode.ERR_NoInputFiles), message);
    }

    public static Diagnostic NoSuchFileOrDirectory(string name) {
        var message = $"{name}: no such file or directory";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_NoSuchFileOrDirectory), message);
    }

    public static Diagnostic NoSuchFile(string name) {
        var message = $"{name}: no such file";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_NoSuchFile), message);
    }
}
