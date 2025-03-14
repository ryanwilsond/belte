using System;
using System.Text;
using Buckle.CodeAnalysis.Symbols;
using Diagnostics;

namespace Repl.Diagnostics;

/// <summary>
/// All predefined error messages that can be used by the Repl.
/// The return value for all methods is a new diagnostic that needs to be manually handled or added to a
/// <see cref="DiagnosticQueue<T>" />.
/// The parameters for all methods allow the error messages to be more dynamic and represent the error more accurately.
/// </summary>
internal static class Error {
    internal static Diagnostic UnknownReplCommand(string line) {
        var message = $"unknown repl command '{line}'";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_UnknownReplCommand), message);
    }

    internal static Diagnostic WrongArgumentCount(string name, string parameterNames) {
        var message = $"invalid number of arguments{Environment.NewLine}usage: #{name} {parameterNames}";
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
        var message = $"invalid argument '{value}'; expected argument of type '{expected}'";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_InvalidArgument), message);
    }

    internal static Diagnostic InvalidOption(object value, object[] options) {
        var message = $"invalid argument '{value}'; expected {FormatList(options)}";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_InvalidOption), message);

        static string FormatList(object[] array) {
            if (array.Length == 2)
                return $"either '{array[0]}' or '{array[1]}'";

            var stringBuilder = new StringBuilder();

            for (var i = 0; i < array.Length; i++) {
                if (i > 0)
                    stringBuilder.Append(", ");

                if (i == array.Length - 1)
                    stringBuilder.Append("or ");

                stringBuilder.Append(array[i]);
            }

            return stringBuilder.ToString();
        }
    }

    internal static Diagnostic NoSuchMethod(string name) {
        var message = $"no such method with the signature '{name}' exists";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_NoSuchMethod), message);
    }

    internal static Diagnostic AmbiguousSignature(string signature, ISymbol[] symbols) {
        var message = new StringBuilder($"'{signature}' is ambiguous between ");

        for (var i = 0; i < symbols.Length; i++) {
            if (i == symbols.Length - 1 && i > 1)
                message.Append(", and ");
            else if (i == symbols.Length - 1)
                message.Append(" and ");
            else if (i > 0)
                message.Append(", ");

            if (symbols[i] is IMethodSymbol f)
                message.Append($"'{f}'");
            else
                message.Append($"'{symbols[i]}'");
        }

        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_AmbiguousSignature), message.ToString());
    }

    internal static Diagnostic FailedILGeneration() {
        var message = $"failed to generate IL: cannot reference locals or globals from previous submissions with the " +
            "'#showIL' toggle on";

        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_FailedILGeneration), message);
    }

    private static DiagnosticInfo ErrorInfo(DiagnosticCode code) {
        return new DiagnosticInfo((int)code, "RE", DiagnosticSeverity.Error);
    }
}
