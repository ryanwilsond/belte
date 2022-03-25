using System;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle {

    internal static class Error {
        public static Diagnostic InvalidType(TextSpan pos, string text, Type type) {
            string msg = $"'{text}' is not a valid {type}";
            return new Diagnostic(DiagnosticType.error, pos, msg);
        }

        public static Diagnostic BadCharacter(int pos, char input) {
            string msg = $"unknown character '{input}'";
            return new Diagnostic(DiagnosticType.error, new TextSpan(pos, 1), msg);
        }

        public static Diagnostic UnexpectedToken(TextSpan pos, SyntaxType unexpected, SyntaxType expected) {
            string msg = $"unexpected token of type {unexpected}, expected {expected}";
            return new Diagnostic(DiagnosticType.error, pos, msg);
        }

        public static Diagnostic InvalidUnaryOperatorUse(TextSpan pos, string op, Type operand) {
            string msg = $"operator '{op}' is not defined for type {operand}";
            return new Diagnostic(DiagnosticType.error, pos, msg);
        }

        public static Diagnostic InvalidBinaryOperatorUse(TextSpan pos, string op, Type left, Type right) {
            string msg = $"operator '{op}' is not defined for types {left} and {right}";
            return new Diagnostic(DiagnosticType.error, pos, msg);
        }

        public static Diagnostic UndefinedName(TextSpan pos, string name) {
            string msg = $"undefined symbol '{name}'";
            return new Diagnostic(DiagnosticType.error, pos, msg);
        }
    }
}
