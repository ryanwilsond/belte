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
    /// <summary>
    /// RE0001. Run `buckle --explain RE0001` on the command line for more info.
    /// </summary>
    internal static Diagnostic UnknownReplCommand(string line) {
        var message = $"unknown repl command '{line}'";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_UnknownReplCommand), message);
    }

    /// <summary>
    /// RE0002. Run `buckle --explain RE0002` on the command line for more info.
    /// </summary>
    internal static Diagnostic WrongArgumentCount(string name, string parameterNames) {
        var message = $"invalid number of arguments{Environment.NewLine}usage: #{name} {parameterNames}";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_WrongArgumentCount), message);
    }

    /// <summary>
    /// RE0003. Run `buckle --explain RE0003` on the command line for more info.
    /// </summary>
    internal static Diagnostic UndefinedSymbol(string name) {
        var message = $"undefined symbol '{name}'";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_UndefinedSymbol), message);
    }

    /// <summary>
    /// RE0004. Run `buckle --explain RE0004` on the command line for more info.
    /// </summary>
    internal static Diagnostic NoSuchFile(string name) {
        var message = $"{name}: no such file";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_NoSuchFile), message);
    }

    /// <summary>
    /// RE0005. Run `buckle --explain RE0005` on the command line for more info.
    /// </summary>
    internal static Diagnostic InvalidArgument(object value, Type expected) {
        var message = $"Invalid argument '{value}'; expected argument of type {expected}";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_InvalidArgument), message);
    }

    /// <summary>
    /// RE0006. Run `buckle --explain RE0006` on the command line for more info.
    /// </summary>
    internal static Diagnostic NoSuchMethod(string name) {
        var message = $"no such method or function with the signature '{name}' exists";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_NoSuchMethod), message);
    }

    /// <summary>
    /// RE0007. Run `buckle --explain RE0007` on the command line for more info.
    /// </summary>
    internal static Diagnostic AmbiguousSignature(string signature, Symbol[] symbols) {
        var message = new StringBuilder($"'{signature}' is ambiguous between ");

        for (int i=0; i<symbols.Length; i++) {
            if (i == symbols.Length - 1 && i > 1)
                message.Append(", and ");
            else if (i == symbols.Length - 1)
                message.Append(" and ");
            else if (i > 0)
                message.Append(", ");

            if (symbols[i] is MethodSymbol f)
                message.Append($"'{f.SignatureNoReturnNoParameterNames()}'");
            else
                message.Append($"'{symbols[i]}'");
        }

        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_AmbiguousSignature), message.ToString());
    }

    /// <summary>
    /// RE0008. Run `buckle --explain RE0008` on the command line for more info.
    /// </summary>
    internal static Diagnostic FailedILGeneration() {
        var message = $"failed to generate IL: cannot reference locals or globals from previous submissions with the " +
            "'#showIL' toggle on";

        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_FailedILGeneration), message);
    }

    private static DiagnosticInfo ErrorInfo(DiagnosticCode code) {
        return new DiagnosticInfo((int)code, "RE", DiagnosticSeverity.Error);
    }
}
