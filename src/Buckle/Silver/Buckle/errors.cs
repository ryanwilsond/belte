using System;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;

namespace Buckle {

    internal static class Error {
        public static Diagnostic InvalidType(TextSpan span, string text, Type type) {
            string msg = $"'{text}' is not a valid {type}";
            return new Diagnostic(DiagnosticType.error, span, msg);
        }

        public static Diagnostic BadCharacter(int pos, char input) {
            string msg = $"unknown character '{input}'";
            return new Diagnostic(DiagnosticType.error, new TextSpan(pos, 1), msg);
        }

        public static Diagnostic UnexpectedToken(TextSpan span, SyntaxType unexpected, SyntaxType expected) {
            string msg = $"unexpected token of type {unexpected}, expected {expected}";
            return new Diagnostic(DiagnosticType.error, span, msg);
        }

        public static Diagnostic InvalidUnaryOperatorUse(TextSpan span, string op, Type operand) {
            string msg = $"operator '{op}' is not defined for type {operand}";
            return new Diagnostic(DiagnosticType.error, span, msg);
        }

        public static Diagnostic InvalidBinaryOperatorUse(TextSpan span, string op, Type left, Type right) {
            string msg = $"operator '{op}' is not defined for types {left} and {right}";
            return new Diagnostic(DiagnosticType.error, span, msg);
        }

        public static Diagnostic UndefinedName(TextSpan span, string name) {
            string msg = $"undefined symbol '{name}'";
            return new Diagnostic(DiagnosticType.error, span, msg);
        }

        public static Diagnostic CannotConvert(TextSpan span, Type from, Type to) {
            string msg = $"cannot convert from type {from} to {to}";
            return new Diagnostic(DiagnosticType.error, span, msg);
        }

        public static Diagnostic AlreadyDeclared(TextSpan span, string name) {
            string msg = $"redefinition of '{name}'";
            return new Diagnostic(DiagnosticType.error, span, msg);
        }

        public static Diagnostic ReadonlyAssign(TextSpan span, string name) {
            string msg = $"assignment of read-only variable '{name}'";
            return new Diagnostic(DiagnosticType.error, span, msg);
        }

        public static Diagnostic AmbiguousElse(TextSpan span) {
            string msg = $"ambiguous what if-statement else-clause belongs to";
            return new Diagnostic(DiagnosticType.error, span, msg);
        }

        public static Diagnostic ExpectedExpression(TextSpan span) {
            string msg = $"expected expression";
            return new Diagnostic(DiagnosticType.error, span, msg);
        }
    }
}
