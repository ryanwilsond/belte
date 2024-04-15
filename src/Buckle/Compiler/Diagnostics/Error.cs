using System.Text;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Diagnostics;

namespace Buckle.Diagnostics;

/// <summary>
/// All predefined error messages that can be used by the compiler.
/// The return value for all methods is a new diagnostic that needs to be manually handled or added to a
/// <see cref="DiagnosticQueue<T>" />.
/// The parameters for all methods allow the error messages to be more dynamic and represent the error more accurately.
/// </summary>
internal static class Error {
    /// <summary>
    /// Temporary error messages.
    /// Once the compiler is finished, this class will be unnecessary.
    /// </summary>
    internal static class Unsupported {
        /// <summary>
        /// BU9000. Run `buckle --explain BU9000` on the command line for more info.
        /// </summary>
        internal static BelteDiagnostic GlobalReturnValue(TextLocation location) {
            var message = "unsupported: global return cannot return a value";
            return new BelteDiagnostic(ErrorInfo(DiagnosticCode.UNS_GlobalReturnValue), location, message);
        }
    }

    /// <summary>
    /// BU0003. Run `buckle --explain BU0003` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic InvalidReference(string reference) {
        var message = $"{reference}: no such file or invalid file type";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_InvalidReference), null, message);
    }

    /// <summary>
    /// BU0004. Run `buckle --explain BU0004` on the command line for more info.
    /// </summary>
    internal static Diagnostic InvalidType(string text, TypeSymbol type) {
        var message = $"'{text}' is not a valid '{type}'";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_InvalidType), message);
    }

    /// <summary>
    /// BU0005. Run `buckle --explain BU0005` on the command line for more info.
    /// </summary>
    internal static Diagnostic BadCharacter(char input) {
        var message = $"unexpected character '{input}'";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_BadCharacter), message);
    }

    /// <summary>
    /// BU0006. Run `buckle --explain BU0006` on the command line for more info.
    /// </summary>
    internal static Diagnostic UnexpectedToken(SyntaxKind unexpected, SyntaxKind? expected = null) {
        string message;

        if (expected is null)
            message = $"unexpected {DiagnosticText(unexpected)}";
        else if (unexpected != SyntaxKind.EndOfFileToken)
            message = $"unexpected {DiagnosticText(unexpected)}, expected {DiagnosticText(expected.Value, false)}";
        else
            message = $"expected {DiagnosticText(expected.Value, false)} at end of input";

        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_UnexpectedToken), message);
    }

    /// <summary>
    /// BU0007. Run `buckle --explain BU0007` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic CannotConvertImplicitly(
        TextLocation location, BoundType from, BoundType to, int argument, bool canAssert) {
        var message =
            $"cannot convert from type '{from}' to '{to}' implicitly; " +
            "an explicit conversion exists (are you missing a cast?)";
        string[] suggestions;

        // % is replaced with all the text at `location`
        if (canAssert)
            suggestions = new string[] { $"({to})%", "(%)!" };
        else
            suggestions = new string[] { $"({to})%" };

        if (argument > 0)
            message = $"argument {argument}: " + message;

        return new BelteDiagnostic(
            ErrorInfo(DiagnosticCode.ERR_CannotConvertImplicitly), location, message, suggestions);
    }

    /// <summary>
    /// BU0008. Run `buckle --explain BU0008` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic InvalidUnaryOperatorUse(TextLocation location, string op, BoundType operand) {
        var message = $"unary operator '{op}' is not defined for type '{operand}'";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_InvalidUnaryOperatorUse), location, message);
    }

    /// <summary>
    /// BU0009. Run `buckle --explain BU0009` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic NamedBeforeUnnamed(TextLocation location) {
        var message = "all named arguments must come after any unnamed arguments";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_NamedBeforeUnnamed), location, message);
    }

    /// <summary>
    /// BU0010. Run `buckle --explain BU0010` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic NamedArgumentTwice(TextLocation location, string name) {
        var message = $"named argument '{name}' cannot be specified multiple times";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_NamedArgumentTwice), location, message);
    }

    /// <summary>
    /// BU0011. Run `buckle --explain BU0011` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic InvalidBinaryOperatorUse(
        TextLocation location, string op, BoundType left, BoundType right, bool isCompound) {
        var operatorWord = isCompound ? "compound" : "binary";
        var message = $"{operatorWord} operator '{op}' is not defined for types '{left}' and '{right}'";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_InvalidBinaryOperatorUse), location, message);
    }

    /// <summary>
    /// BU0012. Run `buckle --explain BU0012` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic GlobalStatementsInMultipleFiles(TextLocation location) {
        var message = "multiple files with global statements creates ambiguous entry point";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_GlobalStatementsInMultipleFiles), location, message);
    }

    /// <summary>
    /// BU0013. Run `buckle --explain BU0013` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic ParameterAlreadyDeclared(TextLocation location, string name) {
        var message = $"cannot reuse parameter name '{name}'; parameter names must be unique";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_ParameterAlreadyDeclared), location, message);
    }

    /// <summary>
    /// BU0014. Run `buckle --explain BU0014` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic InvalidMain(TextLocation location) {
        var message = "invalid main signature: must return void or int and take in no arguments or take in " +
            "'int! argc, string[]! argv'";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_InvalidMain), location, message);
    }

    /// <summary>
    /// BU0015. Run `buckle --explain BU0015` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic NoSuchParameter(
        TextLocation location, string methodName, string parameterName, bool hasOverloads) {
        var methodWord = hasOverloads ? "the best overload for" : "method";
        var message = $"{methodWord} '{methodName}' does not have a parameter named '{parameterName}'";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_NoSuchParameter), location, message);
    }

    /// <summary>
    /// BU0016. Run `buckle --explain BU0016` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic MainAndGlobals(TextLocation location) {
        var message = "declaring a main method and using global statements creates ambiguous entry point";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_MainAndGlobals), location, message);
    }

    /// <summary>
    /// BU0017. Run `buckle --explain BU0017` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic UndefinedSymbol(TextLocation location, string name) {
        var message = $"undefined symbol '{name}'";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_UndefinedSymbol), location, message);
    }

    /// <summary>
    /// BU0018. Run `buckle --explain BU0018` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic MethodAlreadyDeclared(TextLocation location, string name, string typeName = null) {
        string message;

        if (typeName is null)
            message = $"redeclaration of method '{name}'";
        else
            message = $"type '{typeName}' already declares a member named '{name}' with the same parameter types";

        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_MethodAlreadyDeclared), location, message);
    }

    /// <summary>
    /// BU0019. Run `buckle --explain BU0019` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic NotAllPathsReturn(TextLocation location) {
        var message = "not all code paths return a value";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_NotAllPathsReturn), location, message);
    }

    /// <summary>
    /// BU0020. Run `buckle --explain BU0020` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic CannotConvert(
        TextLocation location, BoundType from, BoundType to, int argument = 0) {
        var message = $"cannot convert from type '{from}' to '{to}'";

        if (argument > 0)
            message = $"argument {argument}: " + message;

        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_CannotConvert), location, message);
    }

    /// <summary>
    /// BU0021. Run `buckle --explain BU0021` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic VariableAlreadyDeclared(TextLocation location, string name, bool isConstant) {
        var variableWord = isConstant ? "constant" : "variable";
        var message = $"{variableWord} '{name}' is already declared in this scope";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_VariableAlreadyDeclared), location, message);
    }

    /// <summary>
    /// BU0022. Run `buckle --explain BU0022` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic ConstantAssignment(TextLocation location, string name, bool isConstantReference) {
        var constantWord = isConstantReference ? "constant reference" : "constant";
        var constantPhrase = isConstantReference ? "with a reference " : "";
        var message = $"'{name}' cannot be assigned to {constantPhrase}as it is a {constantWord}";

        if (name is null)
            message = "cannot assign to a constant";

        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_ConstantAssignment), location, message);
    }

    /// <summary>
    /// BU0023. Run `buckle --explain BU0023` on the command line for more info.
    /// </summary>
    internal static Diagnostic AmbiguousElse() {
        var message = "ambiguous which if-statement this else-clause belongs to; use curly braces";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_AmbiguousElse), message);
    }

    /// <summary>
    /// BU0024. Run `buckle --explain BU0024` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic NoValue(TextLocation location) {
        var message = "expression must have a value";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_NoValue), location, message);
    }

    /// <summary>
    /// BU0025. Run `buckle --explain BU0025` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic CannotApplyIndexing(TextLocation location, BoundType type) {
        var message = $"cannot apply indexing with [] to an expression of type '{type}'";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_CannotApplyIndexing), location, message);
    }

    /// <summary>
    /// BU0027. Run `buckle --explain BU0027` on the command line for more info.
    /// </summary>
    internal static Diagnostic UnterminatedString() {
        var message = "unterminated string literal";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_UnterminatedString), message);
    }

    /// <summary>
    /// BU0028. Run `buckle --explain BU0028` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic UndefinedMethod(TextLocation location, string name, bool isInterpreter = false) {
        var message = $"undefined method '{name}'";

        if (isInterpreter)
            message += "; when interpreting all methods must be defined before use";

        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_UndefinedMethod), location, message);
    }

    /// <summary>
    /// BU0029. Run `buckle --explain BU0029` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic IncorrectArgumentCount(
        TextLocation location, string name, int expected, int defaultExpected, int actual, bool isTemplate) {
        var argWord = expected == 1 ? "argument" : "arguments";

        if (isTemplate)
            argWord = "template " + argWord;

        var expectWord = defaultExpected == 0
            ? "expects"
            : actual < expected - defaultExpected ? "expects at least" : "expects at most";

        var expectedNumber = actual < expected - defaultExpected ? expected - defaultExpected : expected;
        var methodWord = isTemplate ? "template" : "method";
        var message = $"{methodWord} '{name}' {expectWord} {expectedNumber} {argWord}, got {actual}";

        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_IncorrectArgumentCount), location, message);
    }

    /// <summary>
    /// BU0030. Run `buckle --explain BU0030` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic TypeAlreadyDeclared(TextLocation location, string name, bool isClass) {
        var classWord = isClass ? "class" : "struct";
        var message = $"{classWord} '{name}' has already been declared in this scope";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_TypeAlreadyDeclared), location, message);
    }

    /// <summary>
    /// BU0031. Run `buckle --explain BU0031` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic DuplicateAttribute(TextLocation location, string name) {
        var message = $"attribute '{name}' has already been applied";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_DuplicateAttribute), location, message);
    }

    /// <summary>
    /// BU0032. Run `buckle --explain BU0032` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic CannotCallNonMethod(TextLocation location, string name) {
        var message = $"called object {(name is null ? "" : $"'{name}' ")}is not a method";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_CannotCallNonMethod), location, message);
    }

    /// <summary>
    /// BU0033. Run `buckle --explain BU0033` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic InvalidExpressionStatement(TextLocation location) {
        var message = "only assignment and call expressions can be used as a statement";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_InvalidExpressionStatement), location, message);
    }

    /// <summary>
    /// BU0034. Run `buckle --explain BU0034` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic UnknownType(TextLocation location, string text) {
        var message = $"unknown type '{text}'";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_UnknownType), location, message);
    }

    /// <summary>
    /// BU0035. Run `buckle --explain BU0035` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic InvalidBreakOrContinue(TextLocation location, string text) {
        var message = $"{text} statements can only be used within a loop";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_InvalidBreakOrContinue), location, message);
    }

    /// <summary>
    /// BU0036. Run `buckle --explain BU0036` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic ReturnOutsideMethod(TextLocation location) {
        var message = "return statements can only be used within a method";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_ReturnOutsideMethod), location, message);
    }

    /// <summary>
    /// BU0037. Run `buckle --explain BU0037` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic UnexpectedReturnValue(TextLocation location) {
        var message = "cannot return a value in a method returning void";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_UnexpectedReturnValue), location, message);
    }

    /// <summary>
    /// BU0038. Run `buckle --explain BU0038` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic MissingReturnValue(TextLocation location) {
        var message = "cannot return without a value in a method returning non-void";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_MissingReturnValue), location, message);
    }

    /// <summary>
    /// BU0039. Run `buckle --explain BU0039` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic NotAVariable(TextLocation location, string name, bool isMethod) {
        var methodWord = isMethod ? "method" : "type";
        var message = $"{methodWord} '{name}' cannot be used as a variable";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_NotAVariable), location, message);
    }

    /// <summary>
    /// BU0040. Run `buckle --explain BU0040` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic NoInitOnImplicit(TextLocation location) {
        var message = "implicitly-typed variable must have initializer";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_NoInitOnImplicit), location, message);
    }

    /// <summary>
    /// BU0041. Run `buckle --explain BU0041` on the command line for more info.
    /// </summary>
    internal static Diagnostic UnterminatedComment() {
        var message = "unterminated multi-line comment";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_UnterminatedComment), message);
    }

    /// <summary>
    /// BU0042. Run `buckle --explain BU0042` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic NullAssignOnImplicit(TextLocation location, bool isConstant) {
        var variableWord = isConstant ? "constant" : "variable";
        var message = $"cannot initialize an implicitly-typed {variableWord} with 'null'";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_NullAssignOnImplicit), location, message);
    }

    /// <summary>
    /// BU0043. Run `buckle --explain BU0043` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic EmptyInitializerListOnImplicit(TextLocation location, bool isConstant) {
        var variableWord = isConstant ? "constant" : "variable";
        var message = $"cannot initialize an implicitly-typed {variableWord} with an empty initializer list";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_EmptyInitializerListOnImplicit), location, message);
    }

    /// <summary>
    /// BU0044. Run `buckle --explain BU0044` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic ImpliedDimensions(TextLocation location) {
        var message = $"collection dimensions on implicit types are inferred making them not necessary in this context";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_ImpliedDimensions), location, message);
    }

    /// <summary>
    /// BU0045. Run `buckle --explain BU0045` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic CannotUseImplicit(TextLocation location) {
        var message = "cannot use implicit-typing in this context";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_CannotUseImplicit), location, message);
    }

    /// <summary>
    /// BU0046. Run `buckle --explain BU0046` on the command line for more info.
    /// </summary>
    internal static Diagnostic NoCatchOrFinally() {
        var message = "try statement must have a catch or finally";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_NoCatchOrFinally), message);
    }

    /// <summary>
    /// BU0047. Run `buckle --explain BU0047` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic MemberMustBeStatic(TextLocation location) {
        var message = "cannot declare instance members in a static class";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_MemberMustBeStatic), location, message);
    }

    /// <summary>
    /// BU0048. Run `buckle --explain BU0048` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic ReferenceNoInitialization(TextLocation location, bool isConstant) {
        var variableWord = isConstant ? "constant" : "variable";
        var message = $"a declaration of a by-reference {variableWord} must have an initializer";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_ReferenceNoInitialization), location, message);
    }

    /// <summary>
    /// BU0049. Run `buckle --explain BU0049` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic ReferenceWrongInitialization(TextLocation location, bool isConstant) {
        var variableWord = isConstant ? "constant" : "variable";
        var message = $"a by-reference {variableWord} must be initialized with a reference";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_ReferenceWrongInitialization), location, message);
    }

    /// <summary>
    /// BU0050. Run `buckle --explain BU0050` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic WrongInitializationReference(TextLocation location, bool isConstant) {
        var variableWord = isConstant ? "constant" : "variable";
        var message = $"cannot initialize a by-value {variableWord} with a reference";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_WrongInitializationReference), location, message);
    }

    /// <summary>
    /// BU0051. Run `buckle --explain BU0051` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic UnknownAttribute(TextLocation location, string text) {
        var message = $"unknown attribute '{text}'";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_UnknownAttribute), location, message);
    }

    /// <summary>
    /// BU0052. Run `buckle --explain BU0052` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic NullAssignOnNotNull(TextLocation location, bool isConstant) {
        var variableWord = isConstant ? "constant" : "variable";
        var message = $"cannot assign 'null' to a non-nullable {variableWord}";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_NullAssignNotNull), location, message);
    }

    /// <summary>
    /// BU0053. Run `buckle --explain BU0053` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic ImpliedReference(TextLocation location) {
        var message = $"implicit types infer reference types making the 'ref' keyword not necessary in this context";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_ImpliedReference), location, message);
    }

    /// <summary>
    /// BU0054. Run `buckle --explain BU0054` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic ReferenceToConstant(TextLocation location, bool isConstant) {
        var variableWord = isConstant ? "constant" : "variable";
        var message = $"cannot assign a reference to a constant to a by-reference {variableWord} expecting a " +
            "reference to a variable";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_ReferenceToConstant), location, message);
    }

    /// <summary>
    /// BU0055. Run `buckle --explain BU0055` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic VoidVariable(TextLocation location) {
        var message = "cannot use void as a type";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_VoidVariable), location, message);
    }

    /// <summary>
    /// BU0056. Run `buckle --explain BU0056` on the command line for more info.
    /// </summary>
    internal static Diagnostic ExpectedToken(string name) {
        var message = $"expected {name}";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_ExpectedToken), message);
    }

    internal static Diagnostic ExpectedToken(SyntaxKind type) {
        return ExpectedToken(DiagnosticText(type));
    }

    /// <summary>
    /// BU0057. Run `buckle --explain BU0057` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic NoMethodOverload(TextLocation location, string name) {
        var message = $"no overload for method '{name}' matches parameter list";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_NoMethodOverload), location, message);
    }

    /// <summary>
    /// BU0058. Run `buckle --explain BU0058` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic AmbiguousMethodOverload(TextLocation location, MethodSymbol[] symbols) {
        var message = new StringBuilder($"call is ambiguous between ");

        for (var i = 0; i < symbols.Length; i++) {
            if (i == symbols.Length - 1 && i > 1)
                message.Append(", and ");
            else if (i == symbols.Length - 1)
                message.Append(" and ");
            else if (i > 0)
                message.Append(", ");

            message.Append($"'{symbols[i].Signature()}'");
        }

        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_AmbiguousMethodOverload), location, message.ToString());
    }

    /// <summary>
    /// BU0059. Run `buckle --explain BU0059` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic CannotIncrement(TextLocation location) {
        var message = "the operand of an increment or decrement operator must be a variable, field, or indexer";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_CannotIncrement), location, message);
    }

    /// <summary>
    /// BU0060. Run `buckle --explain BU0060` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic InvalidTernaryOperatorUse(
        TextLocation location, string op, BoundType left, BoundType center, BoundType right) {
        var message = $"ternary operator '{op}' is not defined for types '{left}', '{center}', and '{right}'";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_InvalidTernaryOperatorUse), location, message);
    }

    /// <summary>
    /// BU0061. Run `buckle --explain BU0061` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic NoSuchMember(TextLocation location, BoundType operand, string text) {
        var message = $"'{operand}' contains no such member '{text}'";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_NoSuchMember), location, message);
    }

    /// <summary>
    /// BU0062. Run `buckle --explain BU0062` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic CannotAssign(TextLocation location) {
        var message = "left side of assignment operation must be a variable, field, or indexer";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_CannotAssign), location, message);
    }

    /// <summary>
    /// BU0063. Run `buckle --explain BU0063` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic CannotOverloadNested(TextLocation location, string name) {
        var message = $"cannot overload nested functions; nested function '{name}' has already been defined";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_CannotOverloadNested), location, message);
    }

    /// <summary>
    /// BU0064. Run `buckle --explain BU0064` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic ConstantToNonConstantReference(TextLocation location, bool isConstant) {
        var variableWord = isConstant ? "constant" : "variable";
        var message = $"cannot assign a reference to a variable to a by-reference {variableWord} expecting a " +
            "reference to a constant";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_ConstantToNonConstantReference), location, message);
    }

    /// <summary>
    /// BU0065. Run `buckle --explain BU0065` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic InvalidPrefixUse(TextLocation location, string op, BoundType operand) {
        var message = $"prefix operator '{op}' is not defined for type '{operand}'";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_InvalidPrefixUse), location, message);
    }

    /// <summary>
    /// BU0066. Run `buckle --explain BU0066` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic InvalidPostfixUse(TextLocation location, string op, BoundType operand) {
        var message = $"postfix operator '{op}' is not defined for type '{operand}'";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_InvalidPostfixUse), location, message);
    }

    /// <summary>
    /// BU0067. Run `buckle --explain BU0067` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic ParameterAlreadySpecified(TextLocation location, string name) {
        var message = $"named argument '{name}' specifies a parameter for which a positional argument has already " +
            "been given";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_ParameterAlreadySpecified), location, message);
    }

    /// <summary>
    /// BU0068. Run `buckle --explain BU0068` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic DefaultMustBeConstant(TextLocation location) {
        var message = "default values for parameters must be compile-time constants";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_DefaultMustBeConstant), location, message);
    }

    /// <summary>
    /// BU0069. Run `buckle --explain BU0069` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic DefaultBeforeNoDefault(TextLocation location) {
        var message = "all optional parameters must be specified after any required parameters";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_DefaultBeforeNoDefault), location, message);
    }

    /// <summary>
    /// BU0070. Run `buckle --explain BU0070` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic ConstantAndVariable(TextLocation location) {
        var message = "cannot mark a type as both constant and variable";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_ConstantAndVariable), location, message);
    }

    /// <summary>
    /// BU0071. Run `buckle --explain BU0071` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic VariableUsingTypeName(TextLocation location, string name, bool isConstant) {
        var variableWord = isConstant ? "constant" : "variable";
        var message = $"{variableWord} name '{name}' is not valid as it is the name of a type in this namespace";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_VariableUsingTypeName), location, message);
    }

    /// <summary>
    /// BU0072. Run `buckle --explain BU0072` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic CannotImplyNull(TextLocation location) {
        var message = "cannot implicitly pass null in a non-nullable context";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_CannotImplyNull), location, message);
    }

    /// <summary>
    /// BU0073. Run `buckle --explain BU0073` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic CannotConvertNull(TextLocation location, BoundType to, int argument = 0) {
        var message = $"cannot convert 'null' to '{to}' because it is a non-nullable type";

        if (argument > 0)
            message = $"argument {argument}: " + message;

        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_CannotConvertNull), location, message);
    }

    /// <summary>
    /// BU0074. Run `buckle --explain BU0074` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic ModifierAlreadyApplied(TextLocation location, string name) {
        var message = $"modifier '{name}' has already been applied to this item";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_ModifierAlreadyApplied), location, message);
    }

    /// <summary>
    /// BU0075. Run `buckle --explain BU0075` on the command line for more info.
    /// </summary>
    internal static Diagnostic CannotUseRef() {
        var message = "cannot use a reference type in this context";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_CannotUseRef), message);
    }

    /// <summary>
    /// BU0076. Run `buckle --explain BU0076` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic DivideByZero(TextLocation location) {
        var message = "cannot divide by zero";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_DivideByZero), location, message);
    }

    /// <summary>
    /// BU0077. Run `buckle --explain BU0077` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic NameUsedInEnclosingScope(TextLocation location, string name) {
        var message = $"a local named '{name}' cannot be declared in this scope because that name is used " +
            "in an enclosing scope to define a local or parameter";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_NameUsedInEnclosingScope), location, message);
    }

    /// <summary>
    /// BU0078. Run `buckle --explain BU0078` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic NullInitializerListOnImplicit(TextLocation location, bool isConstant) {
        var variableWord = isConstant ? "constant" : "variable";
        var message = $"cannot initialize an implicitly-typed {variableWord} with an " +
            "initializer list only containing 'null'";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_NullInitializerListOnImplicit), location, message);
    }

    /// <summary>
    /// BU0079. Run `buckle --explain BU0079` on the command line for more info.
    /// </summary>
    internal static Diagnostic UnrecognizedEscapeSequence(char escapeChar) {
        var message = $"unrecognized escape sequence '\\{escapeChar}'";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_UnrecognizedEscapeSequence), message);
    }

    /// <summary>
    /// BU0080. Run `buckle --explain BU0080` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic PrimitivesDoNotHaveMembers(TextLocation location) {
        var message = "primitive types do not contain any members";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_PrimitivesDoNotHaveMembers), location, message);
    }

    /// <summary>
    /// BU0081. Run `buckle --explain BU0081` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic CannotConstructPrimitive(TextLocation location, string name) {
        var message = $"type '{name}' is a primitive; primitives cannot be created with constructors";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_CannotConstructPrimitive), location, message);
    }

    /// <summary>
    /// BU0082. Run `buckle --explain BU0082` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic NoTemplateOverload(TextLocation location, string name) {
        var message = $"no overload for template '{name}' matches template argument list";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_NoTemplateOverload), location, message);
    }

    /// <summary>
    /// BU0083. Run `buckle --explain BU0083` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic AmbiguousTemplateOverload(TextLocation location, NamedTypeSymbol[] symbols) {
        var message = new StringBuilder($"template is ambiguous between ");

        for (var i = 0; i < symbols.Length; i++) {
            if (i == symbols.Length - 1 && i > 1)
                message.Append(", and ");
            else if (i == symbols.Length - 1)
                message.Append(" and ");
            else if (i > 0)
                message.Append(", ");

            message.Append($"'{symbols[i].Signature()}'");
        }

        return new BelteDiagnostic(
            ErrorInfo(DiagnosticCode.ERR_AmbiguousTemplateOverload),
            location,
            message.ToString()
        );
    }

    /// <summary>
    /// BU0084. Run `buckle --explain BU0084` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic CannotUseStruct(TextLocation location) {
        var message = "cannot use structs outside of a low-level context";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_CannotUseStruct), location, message);
    }

    /// <summary>
    /// BU0085. Run `buckle --explain BU0085` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic CannotUseThis(TextLocation location) {
        var message = "cannot use 'this' outside of a class";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_CannotUseThis), location, message);
    }

    /// <summary>
    /// BU0086. Run `buckle --explain BU0086` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic IncorrectConstructorName(TextLocation location, string name) {
        var message = "constructor name must match the name of the enclosing class; " +
            $"in this case constructors must be named '{name}'";

        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_IncorrectConstructorName), location, message);
    }

    /// <summary>
    /// BU0087. Run `buckle --explain BU0087` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic NoConstructorOverload(TextLocation location, string name) {
        var message = $"type '{name}' does not contain a constructor that matches the parameter list";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_NoConstructorOverload), location, message);
    }

    /// <summary>
    /// BU0088. Run `buckle --explain BU0088` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic InvalidModifier(TextLocation location, string name) {
        var message = $"modifier '{name}' is not valid for this item";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_InvalidModifier), location, message);
    }

    internal static Diagnostic InvalidModifier(string name) {
        var message = $"modifier '{name}' is not valid for this item";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_InvalidModifier), message);
    }

    /// <summary>
    /// BU0089. Run `buckle --explain BU0089` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic InvalidInstanceReference(TextLocation location, string name, string typeName) {
        var message = $"member '{name}' cannot be accessed with an instance reference; " +
            "qualify it with the type name instead";
        var suggestion = $"{typeName}.{name}";

        return new BelteDiagnostic(
            ErrorInfo(DiagnosticCode.ERR_InvalidInstanceReference), location, message, [suggestion]
        );
    }

    /// <summary>
    /// BU0090. Run `buckle --explain BU0090` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic InvalidStaticReference(TextLocation location, string name) {
        var message = $"an object reference is required for non-static member '{name}'";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_InvalidStaticReference), location, message);
    }

    /// <summary>
    /// BU0091. Run `buckle --explain BU0091` on the command line for more info.
    /// </summary>
    internal static Diagnostic CannotInitializeInStructs() {
        var message = "cannot initialize fields in structure definitions";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_CannotInitializeInStructs), message);
    }

    /// <summary>
    /// BU0092. Run `buckle --explain BU0092` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic MultipleMains(TextLocation location) {
        var message = "cannot have multiple 'Main' entry points";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_MultipleMains), location, message);
    }

    /// <summary>
    /// BU0093. Run `buckle --explain BU0093` on the command line for more info.
    /// </summary>
    internal static Diagnostic InvalidAttributes() {
        var message = "attributes are not valid in this context";
        return new Diagnostic(ErrorInfo(DiagnosticCode.ERR_InvalidAttributes), message);
    }

    /// <summary>
    /// BU0094. Run `buckle --explain BU0094` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic TemplateNotExpected(TextLocation location, string name) {
        var message = $"item '{name}' does not expect any template arguments";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_TemplateNotExpected), location, message);
    }

    /// <summary>
    /// BU0095. Run `buckle --explain BU0095` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic TemplateMustBeConstant(TextLocation location) {
        var message = "template argument must be a compile-time constant";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_TemplateMustBeConstant), location, message);
    }

    /// <summary>
    /// BU0096. Run `buckle --explain BU0096` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic CannotReferenceNonField(TextLocation location) {
        var message = "cannot reference non-field or non-variable item";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_CannotReferenceNonField), location, message);
    }

    /// <summary>
    /// BU0097. Run `buckle --explain BU0097` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic CannotUseType(TextLocation location, BoundType type) {
        var message = $"'{type}' is a type, which is not valid in this context";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_CannotUseType), location, message);
    }

    /// <summary>
    /// BU0098. Run `buckle --explain BU0098` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic StaticConstructor(TextLocation location) {
        var message = $"static classes cannot have constructors";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_StaticConstructor), location, message);
    }

    /// <summary>
    /// BU0099. Run `buckle --explain BU0099` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic StaticVariable(TextLocation location) {
        var message = $"cannot declare a variable with a static type";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_StaticVariable), location, message);
    }

    /// <summary>
    /// BU0100. Run `buckle --explain BU0100` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic CannotConstructStatic(TextLocation location, string name) {
        var message = $"cannot create an instance of the static class '{name}'";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_CannotConstructStatic), location, message);
    }

    /// <summary>
    /// BU0101. Run `buckle --explain BU0101` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic StaticAndConst(TextLocation location) {
        var message = $"cannot mark member as both static and constant";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_StaticAndConst), location, message);
    }

    /// <summary>
    /// BU0102. Run `buckle --explain BU0102` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic AssignmentInConstMethod(TextLocation location) {
        var message = $"cannot assign to an instance member in a method marked as constant";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_AssignmentInConstMethod), location, message);
    }

    /// <summary>
    /// BU0103. Run `buckle --explain BU0103` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic NonConstantCallInConstant(TextLocation location, string name) {
        var message = $"cannot call non-constant method '{name}' in a method marked as constant";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_NonConstantCallInConstant), location, message);
    }

    /// <summary>
    /// BU0104. Run `buckle --explain BU0104` on the command line for more info.
    /// </summary>
    internal static BelteDiagnostic NonConstantCallOnConstant(TextLocation location, string name) {
        var message = $"cannot call non-constant method '{name}' on constant";
        return new BelteDiagnostic(ErrorInfo(DiagnosticCode.ERR_NonConstantCallOnConstant), location, message);
    }

    private static DiagnosticInfo ErrorInfo(DiagnosticCode code) {
        return new DiagnosticInfo((int)code, "BU", DiagnosticSeverity.Error);
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

            foreach (var c in type.ToString().Substring(0, type.ToString().Length - 5)) {
                if (char.IsUpper(c))
                    text.Append(' ');

                text.Append(char.ToLower(c));
            }

            return text.Remove(0, 1).ToString();
        } else {
            return type.ToString().ToLower();
        }
    }
}
