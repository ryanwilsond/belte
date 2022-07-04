using Diagnostics;

namespace Repl.Diagnostics;

// TODO: fix code duplication with ErrorInfo and FatalErrorInfo

internal static class Error {
    private static DiagnosticInfo ErrorInfo(DiagnosticCode code) {
        return new DiagnosticInfo((int)code, "RE", DiagnosticType.Error);
    }

    private static DiagnosticInfo FatalErrorInfo(DiagnosticCode code) {
        return new DiagnosticInfo((int)code, "RE", DiagnosticType.Fatal);
    }

    internal static Diagnostic UnknownReplCommand(string line) {
        var message = $"unknown repl command '{line}'";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_UnknownReplCommand), message);
    }

    internal static Diagnostic WrongArgumentCount(string name, string parameterNames) {
        var message = $"invalid number of arguments\nusage: #{name} {parameterNames}";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_WrongArgumentCount), message);
    }

    internal static Diagnostic UndefinedSymbol(string name) {
        var message = $"undefined symbol '{name}'";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_UndefinedSymbol), message);
    }

    internal static Diagnostic NoSuchFile(string name) {
        var message = $"{name}: no such file";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_NoSuchFile), message);
    }
}
