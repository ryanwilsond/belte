using System;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;

namespace Buckle {

    internal static class Error {
        public static string DiagnosticText(SyntaxType type) {
            string factValue = SyntaxFacts.GetText(type);
            if (factValue != null) return "'" + factValue + "'";

            if (type.ToString().EndsWith("_STATEMENT")) return "statement";
            else if (type.ToString().EndsWith("_EXPRESSION")) return "expression";
            else if (type.ToString().EndsWith("_KEYWORD")) return "keyword";
            else return type.ToString().ToLower();
        }

        public static Diagnostic InvalidType(TextSpan span, string text, TypeSymbol type) {
            string msg = $"'{text}' is not a valid '{type}'";
            return new Diagnostic(DiagnosticType.Error, span, msg);
        }

        public static Diagnostic BadCharacter(int position, char input) {
            string msg = $"unknown character '{input}'";
            return new Diagnostic(DiagnosticType.Error, new TextSpan(position, 1), msg);
        }

        public static Diagnostic UnexpectedToken(TextSpan span, SyntaxType unexpected, SyntaxType expected) {
            string msg;

            if (unexpected != SyntaxType.EOF)
                msg = $"unexpected token {DiagnosticText(unexpected)}, expected {DiagnosticText(expected)}";
            else
                msg = $"expected {DiagnosticText(expected)} at end of input";

            return new Diagnostic(DiagnosticType.Error, span, msg);
        }

        public static Diagnostic InvalidUnaryOperatorUse(TextSpan span, string op, TypeSymbol operand) {
            string msg = $"operator '{op}' is not defined for type '{operand}'";
            return new Diagnostic(DiagnosticType.Error, span, msg);
        }

        public static Diagnostic InvalidBinaryOperatorUse(TextSpan span, string op, TypeSymbol left, TypeSymbol right) {
            string msg = $"operator '{op}' is not defined for types '{left}' and '{right}'";
            return new Diagnostic(DiagnosticType.Error, span, msg);
        }

        public static Diagnostic UndefinedName(TextSpan span, string name) {
            string msg = $"undefined symbol '{name}'";
            return new Diagnostic(DiagnosticType.Error, span, msg);
        }

        public static Diagnostic CannotConvert(TextSpan span, TypeSymbol from, TypeSymbol to) {
            string msg = $"cannot convert from type '{from}' to '{to}'";
            return new Diagnostic(DiagnosticType.Error, span, msg);
        }

        public static Diagnostic AlreadyDeclared(TextSpan span, string name) {
            string msg = $"redefinition of '{name}'";
            return new Diagnostic(DiagnosticType.Error, span, msg);
        }

        public static Diagnostic ReadonlyAssign(TextSpan span, string name) {
            string msg = $"assignment of read-only variable '{name}'";
            return new Diagnostic(DiagnosticType.Error, span, msg);
        }

        public static Diagnostic AmbiguousElse(TextSpan span) {
            string msg = $"ambiguous what if-statement else-clause belongs to";
            return new Diagnostic(DiagnosticType.Error, span, msg);
        }

        public static Diagnostic NoValue(TextSpan span) {
            string msg = $"expression must have a value";
            return new Diagnostic(DiagnosticType.Error, span, msg);
        }

        public static Diagnostic ExpectedExpression(TextSpan span) {
            string msg = $"expected expression";
            return new Diagnostic(DiagnosticType.Error, span, msg);
        }

        public static Diagnostic ExpectedStatement(TextSpan span) {
            string msg = $"expected statement";
            return new Diagnostic(DiagnosticType.Error, span, msg);
        }

        public static Diagnostic UnterminatedString(TextSpan span) {
            string msg = $"unterminated string literal";
            return new Diagnostic(DiagnosticType.Error, span, msg);
        }

        public static Diagnostic UndefinedFunction(TextSpan span, string name) {
            string msg = $"undefined function '{name}'";
            return new Diagnostic(DiagnosticType.Error, span, msg);
        }

        public static Diagnostic IncorrectArgumentsCount(TextSpan span, string name, int expected, int actual) {
            string msg = $"function '{name}' expects {expected} arguments, got {actual}";
            return new Diagnostic(DiagnosticType.Error, span, msg);
        }

        public static Diagnostic UnexpectedType(TextSpan span, TypeSymbol lType) {
            string msg = $"unexpected type '{lType}'";
            return new Diagnostic(DiagnosticType.Error, span, msg);
        }

        public static Diagnostic InvalidArgumentType(
                TextSpan span, string parameterName, TypeSymbol expected, TypeSymbol actual) {
            string msg = $"parameter '{parameterName}' expected argument of type '{expected}', got '{actual}'";
            return new Diagnostic(DiagnosticType.Error, span, msg);
        }

        public static Diagnostic CannotCallNonFunction(TextSpan span, string text) {
            string msg = $"called object '{text}' is not a function";
            return new Diagnostic(DiagnosticType.Error, span, msg);
        }

        public static Diagnostic UnknownType(TextSpan span, string text) {
            string msg = $"unknown type '{text}'";
            return new Diagnostic(DiagnosticType.Error, span, msg);
        }

        public static Diagnostic CannotConvertImplicitly(TextSpan span, TypeSymbol from, TypeSymbol to) {
            string msg =
                $"cannot convert from type '{from}' to '{to}'. An explicit conversion exists (are you missing a cast?)";
            return new Diagnostic(DiagnosticType.Error, span, msg);
        }
    }
}
