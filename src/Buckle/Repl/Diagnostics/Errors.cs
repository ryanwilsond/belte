using System.Text;
using Buckle.CodeAnalysis.Symbols;
using Diagnostics;

namespace Repl.Diagnostics;

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

    internal static Diagnostic InvalidArgument(object value, Type expected) {
        var message = $"Invalid argument '{value}'; expected argument of type {expected}";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_InvalidArgument), message);
    }

    internal static Diagnostic NoSuchFunction(string name) {
        var message = $"no such function with the signature '{name}' exists";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_NoSuchFunction), message);
    }

    internal static Diagnostic AmbiguousSignature(string signature, Symbol[] symbols) {
        var message = new StringBuilder($"'{signature}' is ambiguous between ");

        for (int i=0; i<symbols.Length; i++) {
            if (i == symbols.Length - 1 && i > 1)
                message.Append(", and ");
            else if (i == symbols.Length - 1)
                message.Append(" and ");
            else if (i > 0)
                message.Append(", ");

            if (symbols[i] is FunctionSymbol f)
                message.Append($"'{f.SignatureNoReturnNoParameterNames()}'");
            else
                message.Append($"'{symbols[i]}'");
        }

        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_AmbiguousSignature), message.ToString());
    }
}
