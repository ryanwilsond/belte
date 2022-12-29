using System;
using System.Linq;
using System.Text;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Diagnostics;
using Mono.Cecil;

namespace Buckle.Diagnostics;

internal static class Error {
    internal static class Unsupported {
        // * Temporary errors messages go here
        // Thus once the compiler is finished this class will be unnecessary
        internal static BelteDiagnostic GlobalReturnValue(TextLocation location) {
            var message = "unsupported: global return cannot return a value";
            return new BelteDiagnostic(ErrorInfo(DiagnosticCode.UNS_GlobalReturnValue), location, message);
        }

        internal static BelteDiagnostic IndependentCompilation() {
            var message = "unsupported: cannot compile independently; must specify '-i', '-d', or '-r'";
            return new BelteDiagnostic(FatalErrorInfo(DiagnosticCode.UNS_IndependentCompilation), message);
        }

        internal static BelteDiagnostic IsWithoutNull() {
            var message = "unsupported: cannot use 'is' or 'isnt' operators against non-null values";
            return new BelteDiagnostic(FatalErrorInfo(DiagnosticCode.UNS_IsWithoutNull), message);
        }
    }

    private static DiagnosticInfo ErrorInfo(DiagnosticCode code) {
        return new DiagnosticInfo((int)code, "BU", DiagnosticType.Error);
    }

    private static DiagnosticInfo FatalErrorInfo(DiagnosticCode code) {
        return new DiagnosticInfo((int)code, "BU", DiagnosticType.Fatal);
    }

    private static string DiagnosticText(SyntaxKind type, bool sayToken = true) {
        var factValue = SyntaxFacts.GetText(type);

        if (factValue != null && type.IsToken() && sayToken)
            return $"token '{factValue}'";
        else if (factValue != null)
            return $"'{factValue}'";

        if (type.ToString().EndsWith("Statement")) {
            return "statement";
        } else if (type.ToString().EndsWith("Expression")) {
            return "expression";
        } else if (type.IsKeyword()) {
            return "keyword";
        } else if (type.IsToken()) {
            var text = new StringBuilder();

            foreach (var c in type.ToString().Substring(0, type.ToString().Length-5)) {
                if (char.IsUpper(c))
                    text.Append(' ');

                text.Append(char.ToLower(c));
            }

            return text.Remove(0, 1).ToString();
        } else {
            return type.ToString().ToLower();
        }
    }

    internal static BelteDiagnostic ExpectedToken(TextLocation location, SyntaxKind type) {
        var message = $"expected {DiagnosticText(type)}";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_ExpectedToken), location, message);
    }

    internal static BelteDiagnostic InvalidReference(string reference) {
        var message = $"{reference}: no such file or invalid file type";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_InvalidReference), null, message);
    }

    internal static BelteDiagnostic InvalidType(TextLocation location, string text, TypeSymbol type) {
        var message = $"'{text}' is not a valid '{type}'";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_InvalidType), location, message);
    }

    internal static BelteDiagnostic BadCharacter(TextLocation location, int position, char input) {
        var message = $"unknown character '{input}'";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_BadCharacter), location, message);
    }

    internal static BelteDiagnostic UnexpectedToken(
        TextLocation location, SyntaxKind unexpected, SyntaxKind? expected = null) {
        string message;

        if (expected == null)
            message = $"unexpected {DiagnosticText(unexpected)}";
        else if (unexpected != SyntaxKind.EndOfFileToken)
            message = $"unexpected {DiagnosticText(unexpected)}, expected {DiagnosticText(expected.Value, false)}";
        else
            message = $"expected {DiagnosticText(expected.Value, false)} at end of input";

        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_UnexpectedToken), location, message);
    }

    internal static BelteDiagnostic InvalidUnaryOperatorUse(TextLocation location, string op, BoundType operand) {
        var message = $"operator '{op}' is not defined for type '{operand}'";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_InvalidUnaryOperatorUse), location, message);
    }

    internal static BelteDiagnostic RequiredTypeNotFound(string buckleName, string metadataName) {
        var message = buckleName != null
            ? $"could not resolve type '{buckleName}' ('{metadataName}') with the given references"
            : $"could not resolve type '{metadataName}' with the given references";

        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_RequiredTypeNotFound), null, message);
    }

    internal static BelteDiagnostic RequiredTypeAmbiguous(
        string buckleName, string metadataName, TypeDefinition[] foundTypes) {
        var assemblyNames = foundTypes.Select(t => t.Module.Assembly.Name.Name);
        var nameList = string.Join(", ", assemblyNames);

        var message = buckleName != null
            ? $"could not resolve type '{buckleName}' ('{metadataName}') with the given references"
            : $"could not resolve type '{metadataName}' with the given references";

        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_RequiredTypeAmbiguous), null, message);
    }

    internal static BelteDiagnostic InvalidBinaryOperatorUse(
        TextLocation location, string op, BoundType left, BoundType right) {
        var message = $"operator '{op}' is not defined for types '{left}' and '{right}'";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_InvalidBinaryOperatorUse), location, message);
    }

    internal static BelteDiagnostic GlobalStatementsInMultipleFiles(TextLocation location) {
        var message = "multiple files with global statements creates ambigous entry point";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_GlobalStatementsInMultipleFiles), location, message);
    }

    internal static BelteDiagnostic ParameterAlreadyDeclared(TextLocation location, string name) {
        var message = $"redefinition of parameter '{name}'";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_ParameterAlreadyDeclared), location, message);
    }

    internal static BelteDiagnostic InvalidMain(TextLocation location) {
        var message = "invalid main signature: must return void or int and take no arguments";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_InvalidMain), location, message);
    }

    internal static BelteDiagnostic RequiredMethodNotFound(
        string typeName, object methodName, string[] parameterTypeNames) {
        string message;

        if (parameterTypeNames == null) {
            message = $"could not resolve method '{typeName}.{methodName}' with the given references";
        } else {
            var parameterList = string.Join(", ", parameterTypeNames);
            message =
                $"could not resolve method '{typeName}.{methodName}({parameterList})' with the given references";
        }

        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_RequiredMethodNotFound), null, message);
    }

    internal static BelteDiagnostic MainAndGlobals(TextLocation location) {
        var message = "declaring a main function and using global statements creates ambigous entry point";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_MainAndGlobals), location, message);
    }

    internal static BelteDiagnostic UndefinedSymbol(TextLocation location, string name) {
        var message = $"undefined symbol '{name}'";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_UndefinedSymbol), location, message);
    }

    internal static BelteDiagnostic MethodAlreadyDeclared(TextLocation location, string name) {
        var message = $"redefinition of method '{name}'";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_MethodAlreadyDeclared), location, message);
    }

    internal static BelteDiagnostic NotAllPathsReturn(TextLocation location) {
        var message = "not all code paths return a value";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_NotAllPathsReturn), location, message);
    }

    internal static BelteDiagnostic CannotConvert(
        TextLocation location, BoundType from, BoundType to, int argument = 0) {
        var message = $"cannot convert from type '{from}' to '{to}'";

        if (argument > 0)
            message = $"argument {argument}: " + message;

        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_CannotConvert), location, message);
    }

    internal static BelteDiagnostic VariableAlreadyDeclared(TextLocation location, string name) {
        var message = $"redefinition of variable '{name}'";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_VariableAlreadyDeclared), location, message);
    }

    internal static BelteDiagnostic ConstantAssignment(TextLocation location, string name) {
        var message = $"assignment of constant variable '{name}'";

        if (name == null)
            message = "assignment of constant";

        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_ConstantAssignment), location, message);
    }

    internal static BelteDiagnostic AmbiguousElse(TextLocation location) {
        var message = "ambiguous what if-statement this else-clause belongs to; use curly braces";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_AmbiguousElse), location, message);
    }

    internal static BelteDiagnostic NoValue(TextLocation location) {
        var message = "expression must have a value";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_NoValue), location, message);
    }

    internal static BelteDiagnostic UnterminatedString(TextLocation location) {
        var message = "unterminated string literal";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_UnterminatedString), location, message);
    }

    internal static BelteDiagnostic UndefinedFunction(TextLocation location, string name) {
        var message = $"undefined function '{name}'";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_UndefinedFunction), location, message);
    }

    internal static BelteDiagnostic IncorrectArgumentCount(
        TextLocation location, string name, int expected, int actual) {
        var argWord = expected == 1 ? "argument" : "arguments";
        var message = $"function '{name}' expects {expected} {argWord}, got {actual}";

        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_IncorrectArgumentCount), location, message);
    }

    internal static BelteDiagnostic CannotCallNonFunction(TextLocation location, string text) {
        var message = $"called object '{text}' is not a function";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_CannotCallNonFunction), location, message);
    }

    internal static BelteDiagnostic InvalidExpressionStatement(TextLocation location) {
        var message = "only assignment and call expressions can be used as a statement";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_InvalidExpressionStatement), location, message);
    }

    internal static BelteDiagnostic UnknownType(TextLocation location, string text) {
        var message = $"unknown type '{text}'";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_UnknownType), location, message);
    }

    internal static BelteDiagnostic CannotConvertImplicitly(
        TextLocation location, BoundType from, BoundType to, int argument = 0) {
        var message =
            $"cannot convert from type '{from}' to '{to}'. " +
            "An explicit conversion exists (are you missing a cast?)";
        var suggestion = $"({to})%"; // % is replaced with all the text at `location`

        if (argument > 0)
            message = $"argument {argument}: " + message;

        return new BelteDiagnostic(
            ErrorInfo(DiagnosticCode.ERR_CannotConvertImplicitly), location, message, suggestion);
    }

    internal static BelteDiagnostic InvalidBreakOrContinue(TextLocation location, string text) {
        var message = $"{text} statement not within a loop";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_InvalidBreakOrContinue), location, message);
    }

    internal static BelteDiagnostic ReturnOutsideFunction(TextLocation location) {
        var message = "return statement not within a function";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_ReturnOutsideFunction), location, message);
    }

    internal static BelteDiagnostic UnexpectedReturnValue(TextLocation location) {
        var message = "return statement with a value, in function returning void";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_UnexpectedReturnValue), location, message);
    }

    internal static BelteDiagnostic MissingReturnValue(TextLocation location) {
        var message = "return statement with no value, in function returning non-void";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_MissingReturnValue), location, message);
    }

    internal static BelteDiagnostic NotAVariable(TextLocation location, string name) {
        var message = $"function '{name}' used as a variable";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_NotAVariable), location, message);
    }

    internal static BelteDiagnostic UnterminatedComment(TextLocation location) {
        var message = "unterminated multi-line comment";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_UnterminatedComment), location, message);
    }

    internal static BelteDiagnostic NullAssignOnImplicit(TextLocation location) {
        var message = "cannot initialize an implicitly-typed variable with 'null'";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_NullAssignOnImplicit), location, message);
    }

    internal static BelteDiagnostic NoInitOnImplicit(TextLocation location) {
        var message = "implicitly-typed variable must have initializer";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_NoInitOnImplicit), location, message);
    }

    internal static BelteDiagnostic EmptyInitializerListOnImplicit(TextLocation location) {
        var message = "cannot initialize an implicitly-typed variable with an empty initializer list";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_EmptyInitializerListOnImplicit), location, message);
    }

    internal static BelteDiagnostic CannotApplyIndexing(TextLocation location, BoundType type) {
        var message = $"cannot apply indexing with [] to an expression of type '{type}'";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_CannotApplyIndexing), location, message);
    }

    internal static BelteDiagnostic VoidVariable(TextLocation location) {
        var message = "cannot use void as a type";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_VoidVariable), location, message);
    }

    internal static BelteDiagnostic ImpliedDimensions(TextLocation location) {
        var message = "collection dimensions on implicitly-typed variables are inferred making them not necessary " +
            "in this context";

        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_ImpliedDimensions), location, message);
    }

    internal static BelteDiagnostic CannotUseImplicit(TextLocation location) {
        var message = "cannot use implicit-typing in this context";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_CannotUseImplicit), location, message);
    }

    internal static BelteDiagnostic NoCatchOrFinally(TextLocation location) {
        var message = "try statement must have a catch or finally";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_NoCatchOrFinally), location, message);
    }

    internal static BelteDiagnostic ExpectedMethodName(TextLocation location) {
        var message = "expected method name";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_ExpectedMethodName), location, message);
    }

    internal static BelteDiagnostic ReferenceNoInitialization(TextLocation location) {
        var message = "reference variable must have an initializer";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_ReferenceNoInitialization), location, message);
    }

    internal static BelteDiagnostic ReferenceWrongInitialization(TextLocation location) {
        var message = "reference variable must be initialized with a reference";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_ReferenceWrongInitialization), location, message);
    }

    internal static BelteDiagnostic WrongInitializationReference(TextLocation location) {
        var message = "cannot initialize a by-value variable with a reference";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_WrongInitializationReference), location, message);
    }

    internal static BelteDiagnostic UnknownAttribute(TextLocation location, string text) {
        var message = $"unknown attribute '{text}'";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_UnknownAttribute), location, message);
    }

    internal static BelteDiagnostic NullAssignOnNotNull(TextLocation location) {
        var message = "cannot assign 'null' to non-nullable variable";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_NullAssignNotNull), location, message);
    }

    internal static BelteDiagnostic MissingReturnStatement(TextLocation location) {
        var message = "missing return statement in inline function";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_MissingReturnStatement), location, message);
    }

    internal static BelteDiagnostic NoOverload(TextLocation location, string name) {
        var message = $"no overload for function '{name}' matches parameter list";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_NoOverload), location, message);
    }

    internal static BelteDiagnostic AmbiguousOverload(TextLocation location, string name) {
        var message = $"multiple overloads for function '{name}' match parameter list";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_AmbiguousOverload), location, message);
    }

    internal static BelteDiagnostic CannotInitialize(TextLocation location) {
        var message = "cannot initialize declared symbol in this context";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_CannotInitialize), location, message);
    }

    internal static BelteDiagnostic InvalidTernaryOperatorUse(
        TextLocation location, string op, BoundType left, BoundType center, BoundType right) {
        var message = $"operator '{op}' is not defined for types '{left}', '{center}', and '{right}'";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_InvalidTernaryOperatorUse), location, message);
    }

    internal static BelteDiagnostic NoSuchMember(TextLocation location, BoundType operand, string text) {
        var message = $"'{operand}' contains no such member '{text}'";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_NoSuchMember), location, message);
    }

    internal static BelteDiagnostic CannotAssign(TextLocation location) {
        var message = "left side of assignment operation must be a variable or field";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_CannotAssign), location, message);
    }

    internal static BelteDiagnostic CannotOverloadNested(TextLocation location, string name) {
        var message = $"cannot overload nested functions; nested function '{name}' has already been defined";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_CannotOverloadNested), location, message);
    }

    internal static BelteDiagnostic StructAlreadyDeclared(TextLocation location, string name) {
        var message = $"redefinition of struct '{name}'";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_StructAlreadyDeclared), location, message);
    }

    internal static BelteDiagnostic DuplicateAttribute(TextLocation location, string name) {
        var message = $"attribute '{name}' has already been applied";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_DuplicateAttribute), location, message);
    }

    internal static BelteDiagnostic ImpliedReference(TextLocation location) {
        var message = $"implicitly-typed variables infer reference types making the 'ref' keyword not necessary " +
            "in this context";

        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_ImpliedReference), location, message);
    }
}
