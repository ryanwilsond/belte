using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;

namespace Buckle {

    internal static class Error {
        internal static class Unsupported {
            // temporary errors messages go here
            // given compiler is finished this will be empty
        }

        public static string DiagnosticText(SyntaxType type) {
            string factValue = SyntaxFacts.GetText(type);
            if (factValue != null) return "'" + factValue + "'";

            if (type.ToString().EndsWith("_STATEMENT")) return "statement";
            else if (type.ToString().EndsWith("_EXPRESSION")) return "expression";
            else if (type.ToString().EndsWith("_KEYWORD")) return "keyword";
            else return type.ToString().ToLower();
        }

        public static Diagnostic InvalidType(TextLocation location, string text, TypeSymbol type) {
            string msg = $"'{text}' is not a valid '{type}'";
            return new Diagnostic(DiagnosticType.Error, location, msg);
        }

        public static Diagnostic BadCharacter(TextLocation location, int position, char input) {
            string msg = $"unknown character '{input}'";
            return new Diagnostic(DiagnosticType.Error, location, msg);
        }

        public static Diagnostic UnexpectedToken(TextLocation location, SyntaxType unexpected, SyntaxType expected) {
            string msg;

            if (unexpected != SyntaxType.EOF)
                msg = $"unexpected token {DiagnosticText(unexpected)}, expected {DiagnosticText(expected)}";
            else
                msg = $"expected {DiagnosticText(expected)} at end of input";

            return new Diagnostic(DiagnosticType.Error, location, msg);
        }

        public static Diagnostic InvalidUnaryOperatorUse(TextLocation location, string op, TypeSymbol operand) {
            string msg = $"operator '{op}' is not defined for type '{operand}'";
            return new Diagnostic(DiagnosticType.Error, location, msg);
        }

        public static Diagnostic InvalidBinaryOperatorUse(TextLocation location, string op, TypeSymbol left, TypeSymbol right) {
            string msg = $"operator '{op}' is not defined for types '{left}' and '{right}'";
            return new Diagnostic(DiagnosticType.Error, location, msg);
        }

        public static Diagnostic ParameterAlreadyDeclared(TextLocation location, string name) {
            string msg = $"redefinition of parameter '{name}'";
            return new Diagnostic(DiagnosticType.Error, location, msg);
        }

        public static Diagnostic UndefinedName(TextLocation location, string name) {
            string msg = $"undefined symbol '{name}'";
            return new Diagnostic(DiagnosticType.Error, location, msg);
        }

        public static Diagnostic FunctionAlreadyDeclared(TextLocation location, string name) {
            string msg = $"redefinition of function '{name}'";
            return new Diagnostic(DiagnosticType.Error, location, msg);
        }

        public static Diagnostic NotAllPathsReturn(TextLocation location) {
            string msg = $"not all code paths return a value";
            return new Diagnostic(DiagnosticType.Error, location, msg);
        }

        public static Diagnostic CannotConvert(TextLocation location, TypeSymbol from, TypeSymbol to) {
            string msg = $"cannot convert from type '{from}' to '{to}'";
            return new Diagnostic(DiagnosticType.Error, location, msg);
        }

        public static Diagnostic AlreadyDeclared(TextLocation location, string name) {
            string msg = $"redefinition of '{name}'";
            return new Diagnostic(DiagnosticType.Error, location, msg);
        }

        public static Diagnostic ReadonlyAssign(TextLocation location, string name) {
            string msg = $"assignment of read-only variable '{name}'";
            return new Diagnostic(DiagnosticType.Error, location, msg);
        }

        public static Diagnostic AmbiguousElse(TextLocation location) {
            string msg = $"ambiguous what if-statement else-clause belongs to";
            return new Diagnostic(DiagnosticType.Error, location, msg);
        }

        public static Diagnostic NoValue(TextLocation location) {
            string msg = $"expression must have a value";
            return new Diagnostic(DiagnosticType.Error, location, msg);
        }

        public static Diagnostic ExpectedExpression(TextLocation location) {
            string msg = $"expected expression";
            return new Diagnostic(DiagnosticType.Error, location, msg);
        }

        public static Diagnostic ExpectedStatement(TextLocation location) {
            string msg = $"expected statement";
            return new Diagnostic(DiagnosticType.Error, location, msg);
        }

        public static Diagnostic UnterminatedString(TextLocation location) {
            string msg = $"unterminated string literal";
            return new Diagnostic(DiagnosticType.Error, location, msg);
        }

        public static Diagnostic UndefinedFunction(TextLocation location, string name) {
            string msg = $"undefined function '{name}'";
            return new Diagnostic(DiagnosticType.Error, location, msg);
        }

        public static Diagnostic IncorrectArgumentsCount(TextLocation location, string name, int expected, int actual) {
            var argWord = expected == 1 ? "argument" : "arguments";
            string msg = $"function '{name}' expects {expected} {argWord}, got {actual}";
            return new Diagnostic(DiagnosticType.Error, location, msg);
        }

        public static Diagnostic UnexpectedType(TextLocation location, TypeSymbol lType) {
            string msg = $"unexpected type '{lType}'";
            return new Diagnostic(DiagnosticType.Error, location, msg);
        }

        public static Diagnostic InvalidArgumentType(
                TextLocation location, int count, string parameterName, TypeSymbol expected, TypeSymbol actual) {
            string msg =
                $"argument {count}: parameter '{parameterName}' expects argument of type '{expected}', got '{actual}'";
            return new Diagnostic(DiagnosticType.Error, location, msg);
        }

        public static Diagnostic CannotCallNonFunction(TextLocation location, string text) {
            string msg = $"called object '{text}' is not a function";
            return new Diagnostic(DiagnosticType.Error, location, msg);
        }

        public static Diagnostic UnknownType(TextLocation location, string text) {
            string msg = $"unknown type '{text}'";
            return new Diagnostic(DiagnosticType.Error, location, msg);
        }

        public static Diagnostic CannotConvertImplicitly(TextLocation location, TypeSymbol from, TypeSymbol to) {
            string msg =
                $"cannot convert from type '{from}' to '{to}'. An explicit conversion exists (are you missing a cast?)";
            string suggestion = $"{to}(%)";
            return new Diagnostic(DiagnosticType.Error, location, msg, suggestion);
        }

        public static Diagnostic InvalidBreakOrContinue(TextLocation location, string text) {
            string msg = $"{text} statement not within a loop";
            return new Diagnostic(DiagnosticType.Error, location, msg);
        }

        public static Diagnostic ReturnOutsideFunction(TextLocation location) {
            string msg = $"return statement not within a function";
            return new Diagnostic(DiagnosticType.Error, location, msg);
        }

        public static Diagnostic UnexpectedReturnValue(TextLocation location) {
            string msg = $"return statement with a value, in function returning void";
            return new Diagnostic(DiagnosticType.Error, location, msg);
        }

        public static Diagnostic MissingReturnValue(TextLocation location) {
            string msg = $"return statement with no value, in function returning non-void";
            return new Diagnostic(DiagnosticType.Error, location, msg);
        }
    }
}
