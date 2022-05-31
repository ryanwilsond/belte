using System.Linq;
using Mono.Cecil;
using Buckle.CodeAnalysis.Text;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Binding;
using System;

namespace Buckle.Diagnostics;

internal static class Error {
    internal static class Unsupported {
        // temporary errors messages go here
        // given compiler is finished this will be empty
        public static Diagnostic GlobalReturnValue(TextLocation location) {
            string message = $"unsupported: global return cannot return a value";
            return new Diagnostic(DiagnosticType.Error, location, message);
        }
    }

    private static string DiagnosticText(SyntaxType type) {
        string factValue = SyntaxFacts.GetText(type);
        if (factValue != null)
            return "'" + factValue + "'";

        if (type.ToString().EndsWith("_STATEMENT"))
            return "statement";
        else if (type.ToString().EndsWith("_EXPRESSION"))
            return "expression";
        else if (type.IsKeyword())
            return "keyword";
        else if (type.IsToken())
            return type.ToString().ToLower().Substring(0, type.ToString().Length-6);
        else
            return type.ToString().ToLower();
    }

    public static Diagnostic InvalidReference(string reference) {
        string message = $"{reference}: no such file or invalid file type";
        return new Diagnostic(DiagnosticType.Error, null, message);
    }

    public static Diagnostic InvalidType(TextLocation location, string text, TypeSymbol type) {
        string message = $"'{text}' is not a valid '{type}'";
        return new Diagnostic(DiagnosticType.Error, location, message);
    }

    public static Diagnostic BadCharacter(TextLocation location, int position, char input) {
        string message = $"unknown character '{input}'";
        return new Diagnostic(DiagnosticType.Error, location, message);
    }

    public static Diagnostic UnexpectedToken(TextLocation location, SyntaxType unexpected, SyntaxType expected) {
        string message;

        if (unexpected != SyntaxType.END_OF_FILE_TOKEN)
            message = $"unexpected token {DiagnosticText(unexpected)}, expected {DiagnosticText(expected)}";
        else
            message = $"expected {DiagnosticText(expected)} at end of input";

        return new Diagnostic(DiagnosticType.Error, location, message);
    }

    public static Diagnostic InvalidUnaryOperatorUse(TextLocation location, string op, BoundTypeClause operand) {
        string message = $"operator '{op}' is not defined for type '{operand}'";
        return new Diagnostic(DiagnosticType.Error, location, message);
    }

    public static Diagnostic RequiredTypeNotFound(string buckleName, string metadataName) {
        string message = buckleName != null
            ? $"could not resolve type '{buckleName}' ('{metadataName}') with the given references"
            : $"could not resolve type '{metadataName}' with the given references";
        return new Diagnostic(DiagnosticType.Error, null, message);
    }

    public static Diagnostic RequiredTypeAmbiguous(
        string buckleName, string metadataName, TypeDefinition[] foundTypes) {
        var assemblyNames = foundTypes.Select(t => t.Module.Assembly.Name.Name);
        var nameList = string.Join(", ", assemblyNames);

        string message = buckleName != null
            ? $"could not resolve type '{buckleName}' ('{metadataName}') with the given references"
            : $"could not resolve type '{metadataName}' with the given references";
        return new Diagnostic(DiagnosticType.Error, null, message);
    }

    public static Diagnostic InvalidBinaryOperatorUse(
        TextLocation location, string op, BoundTypeClause left, BoundTypeClause right) {
        string message = $"operator '{op}' is not defined for types '{left}' and '{right}'";
        return new Diagnostic(DiagnosticType.Error, location, message);
    }

    public static Diagnostic GlobalStatementsInMultipleFiles(TextLocation location) {
        string message = "multiple files with global statements creates ambigous entry point";
        return new Diagnostic(DiagnosticType.Error, location, message);
    }

    public static Diagnostic ParameterAlreadyDeclared(TextLocation location, string name) {
        string message = $"redefinition of parameter '{name}'";
        return new Diagnostic(DiagnosticType.Error, location, message);
    }

    public static Diagnostic InvalidMain(TextLocation location) {
        string message = "invalid main signature: must return void or int and take no arguments";
        return new Diagnostic(DiagnosticType.Error, location, message);
    }

    public static Diagnostic RequiredMethodNotFound(string typeName, object methodName, string[] parameterTypeNames) {
        string message;

        if (parameterTypeNames == null) {
            message = $"could not resolve method '{typeName}.{methodName}' with the given references";
        } else {
            var parameterList = string.Join(", ", parameterTypeNames);
            message =
                $"could not resolve method '{typeName}.{methodName}({parameterList})' with the given references";
        }

        return new Diagnostic(DiagnosticType.Fatal, null, message);
    }

    public static Diagnostic MainAndGlobals(TextLocation location) {
        string message = "declaring a main function and using global statements creates ambigous entry point";
        return new Diagnostic(DiagnosticType.Error, location, message);
    }

    public static Diagnostic UndefinedName(TextLocation location, string name) {
        string message = $"undefined symbol '{name}'";
        return new Diagnostic(DiagnosticType.Error, location, message);
    }

    public static Diagnostic FunctionAlreadyDeclared(TextLocation location, string name) {
        string message = $"redefinition of function '{name}'";
        return new Diagnostic(DiagnosticType.Error, location, message);
    }

    public static Diagnostic NotAllPathsReturn(TextLocation location) {
        string message = "not all code paths return a value";
        return new Diagnostic(DiagnosticType.Error, location, message);
    }

    public static Diagnostic CannotConvert(TextLocation location, BoundTypeClause from, BoundTypeClause to) {
        string message = $"cannot convert from type '{from}' to '{to}'";
        return new Diagnostic(DiagnosticType.Error, location, message);
    }

    public static Diagnostic AlreadyDeclared(TextLocation location, string name) {
        string message = $"redefinition of '{name}'";
        return new Diagnostic(DiagnosticType.Error, location, message);
    }

    public static Diagnostic ConstAssign(TextLocation location, string name) {
        string message = $"assignment of constant variable '{name}'";
        return new Diagnostic(DiagnosticType.Error, location, message);
    }

    public static Diagnostic AmbiguousElse(TextLocation location) {
        string message = "ambiguous what if-statement else-clause belongs to";
        return new Diagnostic(DiagnosticType.Error, location, message);
    }

    public static Diagnostic NoValue(TextLocation location) {
        string message = "expression must have a value";
        return new Diagnostic(DiagnosticType.Error, location, message);
    }

    public static Diagnostic ExpectedExpression(TextLocation location) {
        string message = "expected expression";
        return new Diagnostic(DiagnosticType.Error, location, message);
    }

    public static Diagnostic ExpectedStatement(TextLocation location) {
        string message = "expected statement";
        return new Diagnostic(DiagnosticType.Error, location, message);
    }

    public static Diagnostic UnterminatedString(TextLocation location) {
        string message = "unterminated string literal";
        return new Diagnostic(DiagnosticType.Error, location, message);
    }

    public static Diagnostic UndefinedFunction(TextLocation location, string name) {
        string message = $"undefined function '{name}'";
        return new Diagnostic(DiagnosticType.Error, location, message);
    }

    public static Diagnostic IncorrectArgumentsCount(TextLocation location, string name, int expected, int actual) {
        var argWord = expected == 1 ? "argument" : "arguments";
        string message = $"function '{name}' expects {expected} {argWord}, got {actual}";
        return new Diagnostic(DiagnosticType.Error, location, message);
    }

    public static Diagnostic UnexpectedType(TextLocation location, BoundTypeClause type) {
        string message = $"unexpected type '{type}'";
        return new Diagnostic(DiagnosticType.Error, location, message);
    }

    public static Diagnostic InvalidArgumentType(
            TextLocation location, int count, string parameterName, BoundTypeClause expected, BoundTypeClause actual) {
        string message =
            $"argument {count}: parameter '{parameterName}' expects argument of type " +
            $"'{expected}', got '{actual}'";

        return new Diagnostic(DiagnosticType.Error, location, message);
    }

    public static Diagnostic CannotCallNonFunction(TextLocation location, string text) {
        string message = $"called object '{text}' is not a function";
        return new Diagnostic(DiagnosticType.Error, location, message);
    }

    public static Diagnostic InvalidExpressionStatement(TextLocation location) {
        string message = "only assignment and call expressions can be used as a statement";
        return new Diagnostic(DiagnosticType.Error, location, message);
    }

    public static Diagnostic UnknownType(TextLocation location, string text) {
        string message = $"unknown type '{text}'";
        return new Diagnostic(DiagnosticType.Error, location, message);
    }

    public static Diagnostic CannotConvertImplicitly(TextLocation location, BoundTypeClause from, BoundTypeClause to) {
        string message =
            $"cannot convert from type '{from}' to '{to}'. " +
            "An explicit conversion exists (are you missing a cast?)";
        string suggestion = $"({to})%"; // % is replaced with all the text at `location`
        return new Diagnostic(DiagnosticType.Error, location, message, suggestion);
    }

    public static Diagnostic InvalidBreakOrContinue(TextLocation location, string text) {
        string message = $"{text} statement not within a loop";
        return new Diagnostic(DiagnosticType.Error, location, message);
    }

    public static Diagnostic ReturnOutsideFunction(TextLocation location) {
        string message = "return statement not within a function";
        return new Diagnostic(DiagnosticType.Error, location, message);
    }

    public static Diagnostic UnexpectedReturnValue(TextLocation location) {
        string message = "return statement with a value, in function returning void";
        return new Diagnostic(DiagnosticType.Error, location, message);
    }

    public static Diagnostic MissingReturnValue(TextLocation location) {
        string message = "return statement with no value, in function returning non-void";
        return new Diagnostic(DiagnosticType.Error, location, message);
    }

    public static Diagnostic NotAVariable(TextLocation location, string name) {
        string message = $"function '{name}' used as a variable";
        return new Diagnostic(DiagnosticType.Error, location, message);
    }

    public static Diagnostic UnterminatedComment(TextLocation location) {
        string message = "unterminated multi-line comment";
        return new Diagnostic(DiagnosticType.Error, location, message);
    }

    public static Diagnostic NullAssignOnImplicit(TextLocation location) {
        string message = "cannot assign 'null' to an implicitly-typed variable";
        return new Diagnostic(DiagnosticType.Error, location, message);
    }

    public static Diagnostic NoInitOnImplicit(TextLocation location) {
        string message = "implicitly-typed variable must have initializer";
        return new Diagnostic(DiagnosticType.Error, location, message);
    }

    public static Diagnostic EmptyInitializerListOnImplicit(TextLocation location) {
        string message = "cannot assign empty initializer list to an implicitly-typed variable";
        return new Diagnostic(DiagnosticType.Error, location, message);
    }

    public static Diagnostic CannotApplyIndexing(TextLocation location, BoundTypeClause type) {
        string message = $"cannot apply indexing with [] to an expression of type '{type}'";
        return new Diagnostic(DiagnosticType.Error, location, message);
    }

    public static Diagnostic VoidVariable(TextLocation location) {
        string message = "cannot use void as a type";
        return new Diagnostic(DiagnosticType.Error, location, message);
    }

    public static Diagnostic ImpliedDimensions(TextLocation location) {
        string message = "collection dimensions are inferred and not necessary";
        return new Diagnostic(DiagnosticType.Error, location, message);
    }

    public static Diagnostic CannotUseImplicit(TextLocation location) {
        string message = "cannot use implicit-typing in this context";
        return new Diagnostic(DiagnosticType.Error, location, message);
    }

    public static Diagnostic NoCatchOrFinally(TextLocation location) {
        string message = "try statement must have a catch or finally";
        return new Diagnostic(DiagnosticType.Error, location, message);
    }

    public static Diagnostic ExpectedMethodName(TextLocation location) {
        string message = "expected method name";
        return new Diagnostic(DiagnosticType.Error, location, message);
    }

    public static Diagnostic ReferenceNoInitialization(TextLocation location) {
        string message = "reference variable must have an initializer";
        return new Diagnostic(DiagnosticType.Error, location, message);
    }

    public static Diagnostic ReferenceWrongInitialization(TextLocation location) {
        string message = "reference variable must be initialized with a reference";
        return new Diagnostic(DiagnosticType.Error, location, message);
    }

    public static Diagnostic WrongInitializationReference(TextLocation location) {
        string message = "cannot initialize variable with reference";
        return new Diagnostic(DiagnosticType.Error, location, message);
    }

    public static Diagnostic UnknownAttribute(TextLocation location, string text) {
        string message = $"unknown attribute '{text}'";
        return new Diagnostic(DiagnosticType.Error, location, message);
    }

    public static Diagnostic NullAssignOnNotNull(TextLocation location) {
        string message = "cannot assign null to non-nullable variable";
        return new Diagnostic(DiagnosticType.Error, location, message);
    }

    public static Diagnostic InconsistentReturnTypes(TextLocation location) {
        string message = "all return statements must return the same type";
        return new Diagnostic(DiagnosticType.Error, location, message);
    }

    public static Diagnostic MissingReturnStatement(TextLocation location) {
        string message = "missing return statement in inline function";
        return new Diagnostic(DiagnosticType.Error, location, message);
    }
}
