using System.Text;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Display;
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
        internal static BelteDiagnostic GlobalReturnValue(TextLocation location) {
            var message = "unsupported: global return cannot return a value";
            return CreateError(DiagnosticCode.UNS_GlobalReturnValue, location, message);
        }

        internal static BelteDiagnostic OverloadedPostfix(TextLocation location) {
            var message = "unsupported: overloaded postfix";
            return CreateError(DiagnosticCode.UNS_OverloadedPostfix, location, message);
        }

        internal static BelteDiagnostic NonTypeTemplate(TextLocation location) {
            var message = "unsupported: cannot declare a non-type template while " +
                "building for .NET or transpiling to C#";
            return CreateError(DiagnosticCode.UNS_NonTypeTemplate, location, message);
        }
    }

    internal static BelteDiagnostic InvalidReference(string reference) {
        var message = $"{reference}: no such file or invalid file type";
        return CreateError(DiagnosticCode.ERR_InvalidReference, null, message);
    }

    internal static Diagnostic InvalidType(string text, TypeSymbol type) {
        var message = $"'{text}' is not a valid '{type}'";
        return CreateError(DiagnosticCode.ERR_InvalidType, message);
    }

    internal static Diagnostic BadCharacter(char input) {
        var message = $"unexpected character '{input}'";
        return CreateError(DiagnosticCode.ERR_BadCharacter, message);
    }

    internal static Diagnostic UnexpectedToken(SyntaxKind unexpected, SyntaxKind? expected = null) {
        string message;

        if (expected is null)
            message = $"unexpected {DiagnosticText(unexpected)}";
        else if (unexpected != SyntaxKind.EndOfFileToken)
            message = $"unexpected {DiagnosticText(unexpected)}, expected {DiagnosticText(expected.Value, false)}";
        else
            message = $"expected {DiagnosticText(expected.Value, false)} at end of input";

        return CreateError(DiagnosticCode.ERR_UnexpectedToken, message);
    }

    internal static Diagnostic UnexpectedToken(SyntaxKind unexpected, SyntaxKind expected1, SyntaxKind expected2) {
        string message;

        if (unexpected == SyntaxKind.EndOfFileToken) {
            message = $"expected {DiagnosticText(expected1, false)} or " +
                $"{DiagnosticText(expected2, false)} at end of input";
        } else {
            message = $"unexpected {DiagnosticText(unexpected)}, " +
                $"expected {DiagnosticText(expected1, false)} or {DiagnosticText(expected2, false)}";
        }

        return CreateError(DiagnosticCode.ERR_UnexpectedToken, message);
    }

    internal static BelteDiagnostic UnexpectedToken(TextLocation location, SyntaxKind kind) {
        var message = $"unexpected {DiagnosticText(kind)}";
        return CreateError(DiagnosticCode.ERR_UnexpectedToken, location, message);
    }

    internal static BelteDiagnostic NoImplicitConversion(TextLocation location, TypeSymbol from, TypeSymbol to) {
        var message = $"cannot convert from type '{from.ToNullOrString()}' to '{to.ToNullOrString()}' implicitly";
        return CreateError(DiagnosticCode.ERR_NoImplicitConversion, location, message);
    }

    internal static BelteDiagnostic CannotConvertImplicitlyArgument(
        TextLocation location,
        TypeSymbol from,
        TypeSymbol to,
        int argument,
        bool canAssert) {
        var message = $"cannot convert from type '{from}' to '{to}' implicitly; an explicit conversion exists (are you missing a cast?)";
        // % is replaced with all the text at `location`
        var suggestions = canAssert ? (string[])[$"({to})%", "(%)!"] : [$"({to})%"];

        if (argument > 0)
            message = $"argument {argument}: " + message;

        return CreateError(DiagnosticCode.ERR_CannotConvertImplicitly, location, message, suggestions);
    }

    internal static BelteDiagnostic CannotConvertImplicitly(TextLocation location, TypeSymbol from, TypeSymbol to) {
        var message = $"cannot convert from type '{from.ToNullOrString()}' to '{to.ToNullOrString()}' implicitly; an explicit conversion exists (are you missing a cast?)";
        var suggestion = $"({to.ToNullOrString()})%";
        return CreateError(DiagnosticCode.ERR_CannotConvertImplicitly, location, message, suggestion);
    }

    internal static BelteDiagnostic CannotConvertImplicitlyNullable(TextLocation location, TypeSymbol from, TypeSymbol to) {
        var message = $"cannot convert from type '{from.ToNullOrString()}' to '{to.ToNullOrString()}' implicitly; an explicit conversion exists (are you missing a cast?)";
        var suggestions = (string[])[$"({to.ToNullOrString()})%", "(%)!"];
        return CreateError(DiagnosticCode.ERR_CannotConvertImplicitlyNullable, location, message, suggestions);
    }

    internal static BelteDiagnostic InvalidUnaryOperatorUse(TextLocation location, string op, TypeSymbol operand) {
        var message = $"unary operator '{op}' is not defined for type '{operand.ToNullOrString()}'";
        return CreateError(DiagnosticCode.ERR_InvalidUnaryOperatorUse, location, message);
    }

    internal static BelteDiagnostic NamedBeforeUnnamed(TextLocation location) {
        var message = "all named arguments must come after any unnamed arguments";
        return CreateError(DiagnosticCode.ERR_NamedBeforeUnnamed, location, message);
    }

    internal static BelteDiagnostic NamedArgumentTwice(TextLocation location, string name) {
        var message = $"named argument '{name}' cannot be specified multiple times";
        return CreateError(DiagnosticCode.ERR_NamedArgumentTwice, location, message);
    }

    internal static BelteDiagnostic InvalidBinaryOperatorUse(TextLocation location, string op, TypeSymbol left, TypeSymbol right) {
        var message = $"binary operator '{op}' is not defined for operands of types '{left.ToNullOrString()}' and '{right.ToNullOrString()}'";
        return CreateError(DiagnosticCode.ERR_InvalidBinaryOperatorUse, location, message);
    }

    internal static BelteDiagnostic GlobalStatementsInMultipleFiles(TextLocation location) {
        var message = "multiple files with global statements creates ambiguous entry point";
        return CreateError(DiagnosticCode.ERR_GlobalStatementsInMultipleFiles, location, message);
    }

    internal static BelteDiagnostic ParameterAlreadyDeclared(TextLocation location, string name) {
        var message = $"cannot reuse parameter name '{name}'; parameter names must be unique";
        return CreateError(DiagnosticCode.ERR_ParameterAlreadyDeclared, location, message);
    }

    internal static BelteDiagnostic BadArgumentName(TextLocation location, string methodName, string name) {
        var message = $"the best overload for '{methodName}' does not have a parameter named '{name}'";
        return CreateError(DiagnosticCode.ERR_BadArgumentName, location, message);
    }

    internal static BelteDiagnostic MainAndGlobals(TextLocation location) {
        var message = "declaring a main method and using global statements creates ambiguous entry point";
        return CreateError(DiagnosticCode.ERR_MainAndGlobals, location, message);
    }

    internal static BelteDiagnostic UndefinedSymbol(TextLocation location, string name) {
        var message = $"undefined symbol '{name}'";
        return CreateError(DiagnosticCode.ERR_UndefinedSymbol, location, message);
    }

    internal static BelteDiagnostic MethodAlreadyDeclared(
        TextLocation location,
        string signature,
        string typeName = null) {
        var message = $"redeclaration of method '{signature}'";

        if (typeName is not null)
            message += $"within type '{typeName}'";

        return CreateError(DiagnosticCode.ERR_MethodAlreadyDeclared, location, message);
    }

    internal static BelteDiagnostic NotAllPathsReturn(TextLocation location) {
        var message = "not all code paths return a value";
        return CreateError(DiagnosticCode.ERR_NotAllPathsReturn, location, message);
    }

    internal static BelteDiagnostic CannotConvert(TextLocation location, TypeSymbol from, TypeSymbol to) {
        var message = $"cannot convert from type '{from.ToNullOrString()}' to '{to.ToNullOrString()}'";
        return CreateError(DiagnosticCode.ERR_CannotConvert, location, message);
    }

    internal static BelteDiagnostic CannotConvertArgument(TextLocation location, TypeSymbol from, TypeSymbol to, int argument) {
        var message = $"argument {argument}: cannot convert from type '{from.ToNullOrString()}' to '{to.ToNullOrString()}'";
        return CreateError(DiagnosticCode.ERR_CannotConvertArgument, location, message);
    }

    internal static BelteDiagnostic VariableAlreadyDeclared(TextLocation location, string name, bool isConstant) {
        var variableWord = isConstant ? "constant" : "variable";
        var message = $"{variableWord} '{name}' is already declared in this scope";
        return CreateError(DiagnosticCode.ERR_VariableAlreadyDeclared, location, message);
    }

    internal static BelteDiagnostic ConstantAssignment(TextLocation location, Symbol symbol) {
        var message = $"cannot assign to '{symbol}' because it is constant";
        return CreateError(DiagnosticCode.ERR_ConstantAssignment, location, message);
    }

    internal static Diagnostic AmbiguousElse() {
        var message = "ambiguous which if-statement this else-clause belongs to; use curly braces";
        return CreateError(DiagnosticCode.ERR_AmbiguousElse, message);
    }

    internal static BelteDiagnostic NoValue(TextLocation location) {
        var message = "expression must have a value";
        return CreateError(DiagnosticCode.ERR_NoValue, location, message);
    }

    internal static BelteDiagnostic CannotApplyIndexing(TextLocation location, TypeSymbol type) {
        var message = $"cannot apply indexing with [] to an expression of type '{type.ToNullOrString()}'";
        return CreateError(DiagnosticCode.ERR_CannotApplyIndexing, location, message);
    }

    internal static Diagnostic UnterminatedString() {
        var message = "unterminated string literal";
        return CreateError(DiagnosticCode.ERR_UnterminatedString, message);
    }

    internal static BelteDiagnostic UndefinedMethod(TextLocation location, string name, bool isInterpreter = false) {
        var message = $"undefined method '{name}'";

        if (isInterpreter)
            message += "; when interpreting all methods must be defined before use";

        return CreateError(DiagnosticCode.ERR_UndefinedMethod, location, message);
    }

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

        return CreateError(DiagnosticCode.ERR_IncorrectArgumentCount, location, message);
    }

    internal static BelteDiagnostic TypeAlreadyDeclared(TextLocation location, string name, bool isClass) {
        var classWord = isClass ? "class" : "struct";
        var message = $"{classWord} '{name}' has already been declared in this scope";
        return CreateError(DiagnosticCode.ERR_TypeAlreadyDeclared, location, message);
    }

    internal static BelteDiagnostic DuplicateAttribute(TextLocation location, string name) {
        var message = $"attribute '{name}' has already been applied";
        return CreateError(DiagnosticCode.ERR_DuplicateAttribute, location, message);
    }

    internal static BelteDiagnostic CannotCallNonMethod(TextLocation location, string name) {
        var message = $"called object {(name is null ? "" : $"'{name}' ")}is not a method";
        return CreateError(DiagnosticCode.ERR_CannotCallNonMethod, location, message);
    }
    internal static BelteDiagnostic InvalidExpressionStatement(TextLocation location) {
        var message = "only assignment and call expressions can be used as a statement";
        return CreateError(DiagnosticCode.ERR_InvalidExpressionStatement, location, message);
    }

    internal static BelteDiagnostic UnknownType(TextLocation location, string text) {
        var message = $"unknown type '{text}'";
        return CreateError(DiagnosticCode.ERR_UnknownType, location, message);
    }

    internal static BelteDiagnostic InvalidBreakOrContinue(TextLocation location) {
        var message = $"break and continue statements can only be used within a loop";
        return CreateError(DiagnosticCode.ERR_InvalidBreakOrContinue, location, message);
    }

    internal static BelteDiagnostic ReturnOutsideMethod(TextLocation location) {
        var message = "return statements can only be used within a method";
        return CreateError(DiagnosticCode.ERR_ReturnOutsideMethod, location, message);
    }

    internal static BelteDiagnostic UnexpectedReturnValue(TextLocation location) {
        var message = "cannot return a value in a method returning void";
        return CreateError(DiagnosticCode.ERR_UnexpectedReturnValue, location, message);
    }

    internal static BelteDiagnostic MissingReturnValue(TextLocation location) {
        var message = "cannot return without a value in a method returning non-void";
        return CreateError(DiagnosticCode.ERR_MissingReturnValue, location, message);
    }

    internal static BelteDiagnostic NotAVariable(TextLocation location, string name, bool isMethod) {
        var methodWord = isMethod ? "method" : "type";
        var message = $"{methodWord} '{name}' cannot be used as a variable";
        return CreateError(DiagnosticCode.ERR_NotAVariable, location, message);
    }

    internal static BelteDiagnostic NoInitOnImplicit(TextLocation location) {
        var message = "implicitly-typed variable must have initializer";
        return CreateError(DiagnosticCode.ERR_NoInitOnImplicit, location, message);
    }

    internal static Diagnostic UnterminatedComment() {
        var message = "unterminated multi-line comment";
        return CreateError(DiagnosticCode.ERR_UnterminatedComment, message);
    }

    internal static BelteDiagnostic NullAssignOnImplicit(TextLocation location, bool isConstant) {
        var variableWord = isConstant ? "constant" : "variable";
        var message = $"cannot initialize an implicitly-typed {variableWord} with 'null'";
        return CreateError(DiagnosticCode.ERR_NullAssignOnImplicit, location, message);
    }

    internal static BelteDiagnostic EmptyInitializerListOnImplicit(TextLocation location, bool isConstant) {
        var variableWord = isConstant ? "constant" : "variable";
        var message = $"cannot initialize an implicitly-typed {variableWord} with an empty initializer list";
        return CreateError(DiagnosticCode.ERR_EmptyInitializerListOnImplicit, location, message);
    }

    internal static BelteDiagnostic ImpliedDimensions(TextLocation location) {
        var message = $"collection dimensions on implicit types are inferred making them not necessary in this context";
        return CreateError(DiagnosticCode.ERR_ImpliedDimensions, location, message);
    }

    internal static BelteDiagnostic CannotUseImplicit(TextLocation location) {
        var message = "cannot use implicit-typing in this context";
        return CreateError(DiagnosticCode.ERR_CannotUseImplicit, location, message);
    }

    internal static Diagnostic NoCatchOrFinally() {
        var message = "try statement must have a catch or finally";
        return CreateError(DiagnosticCode.ERR_NoCatchOrFinally, message);
    }

    internal static BelteDiagnostic MemberMustBeStatic(TextLocation location) {
        var message = "cannot declare instance members in a static class";
        return CreateError(DiagnosticCode.ERR_MemberMustBeStatic, location, message);
    }

    internal static Diagnostic ExpectedOverloadableOperator() {
        var message = $"expected overloadable unary, arithmetic, equality, or comparison operator";
        return CreateError(DiagnosticCode.ERR_ExpectedOverloadableOperator, message);
    }

    internal static BelteDiagnostic InitializeByReferenceWithByValue(TextLocation location) {
        var message = $"a by-reference data container must be initialized with a reference";
        return CreateError(DiagnosticCode.ERR_InitializeByReferenceWithByValue, location, message);
    }

    internal static BelteDiagnostic InitializeByValueWithByReference(TextLocation location) {
        var message = $"cannot initialize a by-value data container with a reference";
        return CreateError(DiagnosticCode.ERR_InitializeByValueWithByReference, location, message);
    }

    internal static BelteDiagnostic UnknownAttribute(TextLocation location, string text) {
        var message = $"unknown attribute '{text}'";
        return CreateError(DiagnosticCode.ERR_UnknownAttribute, location, message);
    }

    internal static BelteDiagnostic NullAssignOnNotNull(TextLocation location, bool isConstant) {
        var variableWord = isConstant ? "constant" : "variable";
        var message = $"cannot assign 'null' to a non-nullable {variableWord}";
        return CreateError(DiagnosticCode.ERR_NullAssignNotNull, location, message);
    }

    internal static BelteDiagnostic ImpliedReference(TextLocation location) {
        var message = $"implicit types infer reference types making the 'ref' keyword not necessary in this context";
        return CreateError(DiagnosticCode.ERR_ImpliedReference, location, message);
    }

    internal static BelteDiagnostic ReferenceToConstant(TextLocation location) {
        var message = $"cannot assign a reference to a constant to a by-reference data container expecting a reference to a variable";
        return CreateError(DiagnosticCode.ERR_ReferenceToConstant, location, message);
    }

    internal static BelteDiagnostic VoidVariable(TextLocation location) {
        var message = "cannot use void as a type";
        return CreateError(DiagnosticCode.ERR_VoidVariable, location, message);
    }

    internal static Diagnostic ExpectedToken(string name) {
        var message = $"expected {name}";
        return CreateError(DiagnosticCode.ERR_ExpectedToken, message);
    }

    internal static Diagnostic ExpectedToken(SyntaxKind type) {
        return ExpectedToken(DiagnosticText(type));
    }

    internal static BelteDiagnostic WrongArgumentCount(TextLocation location, string name, int argCount) {
        var message = $"no overload for method '{name}' takes {argCount} arguments";
        return CreateError(DiagnosticCode.ERR_WrongArgumentCount, location, message);
    }

    internal static BelteDiagnostic AmbiguousMethodOverload(TextLocation location, MethodSymbol[] symbols) {
        var message = new StringBuilder($"call is ambiguous between ");

        for (var i = 0; i < symbols.Length; i++) {
            if (i == symbols.Length - 1 && i > 1)
                message.Append(", and ");
            else if (i == symbols.Length - 1)
                message.Append(" and ");
            else if (i > 0)
                message.Append(", ");

            message.Append($"'{symbols[i]}'");
        }

        return CreateError(DiagnosticCode.ERR_AmbiguousMethodOverload, location, message.ToString());
    }

    internal static BelteDiagnostic CannotIncrement(TextLocation location) {
        var message = "the operand of an increment or decrement operator must be a variable, field, or indexer";
        return CreateError(DiagnosticCode.ERR_CannotIncrement, location, message);
    }

    internal static BelteDiagnostic InvalidTernaryOperatorUse(
        TextLocation location, string op, TypeSymbol left, TypeSymbol center, TypeSymbol right) {
        var message = $"ternary operator '{op}' is not defined for types '{left}', '{center}', and '{right}'";
        return CreateError(DiagnosticCode.ERR_InvalidTernaryOperatorUse, location, message);
    }

    internal static BelteDiagnostic NoSuchMember(TextLocation location, TypeSymbol operand, string text) {
        var message = $"'{operand.ToNullOrString()}' contains no such member '{text}'";
        return CreateError(DiagnosticCode.ERR_NoSuchMember, location, message);
    }

    internal static BelteDiagnostic NoSuchMember(TextLocation location, BoundExpression operand, string text) {
        var message = $"'{operand}' contains no such member '{text}'";
        return CreateError(DiagnosticCode.ERR_NoSuchMember, location, message);
    }

    internal static BelteDiagnostic AssignableLValueExpected(TextLocation location) {
        var message = "left side of assignment operation must be a variable, parameter, field, or indexer";
        return CreateError(DiagnosticCode.ERR_AssignableLValueExpected, location, message);
    }

    internal static BelteDiagnostic CannotOverloadNested(TextLocation location, string name) {
        var message = $"cannot overload nested functions; nested function '{name}' has already been defined";
        return CreateError(DiagnosticCode.ERR_CannotOverloadNested, location, message);
    }

    internal static BelteDiagnostic ConstantToNonConstantReference(TextLocation location, bool isConstant) {
        var variableWord = isConstant ? "constant" : "variable";
        var message = $"cannot assign a reference to a variable to a by-reference {variableWord} expecting a " +
            "reference to a constant";
        return CreateError(DiagnosticCode.ERR_ConstantToNonConstantReference, location, message);
    }

    internal static BelteDiagnostic InvalidPrefixUse(TextLocation location, string op, TypeSymbol operand) {
        var message = $"prefix operator '{op}' is not defined for type '{operand.ToNullOrString()}'";
        return CreateError(DiagnosticCode.ERR_InvalidPrefixUse, location, message);
    }

    internal static BelteDiagnostic InvalidPostfixUse(TextLocation location, string op, TypeSymbol operand) {
        var message = $"postfix operator '{op}' is not defined for type '{operand.ToNullOrString()}'";
        return CreateError(DiagnosticCode.ERR_InvalidPostfixUse, location, message);
    }

    internal static BelteDiagnostic ParameterAlreadySpecified(TextLocation location, string name) {
        var message = $"named argument '{name}' specifies a parameter for which a positional argument has already " +
            "been given";
        return CreateError(DiagnosticCode.ERR_ParameterAlreadySpecified, location, message);
    }

    internal static BelteDiagnostic DefaultMustBeConstant(TextLocation location) {
        var message = "default values for parameters must be compile-time constants";
        return CreateError(DiagnosticCode.ERR_DefaultMustBeConstant, location, message);
    }

    internal static BelteDiagnostic DefaultBeforeNoDefault(TextLocation location) {
        var message = "all optional parameters must be specified after any required parameters";
        return CreateError(DiagnosticCode.ERR_DefaultBeforeNoDefault, location, message);
    }

    internal static BelteDiagnostic ConstantAndVariable(TextLocation location) {
        var message = "cannot mark a type as both constant and variable";
        return CreateError(DiagnosticCode.ERR_ConstantAndVariable, location, message);
    }

    internal static BelteDiagnostic VariableUsingTypeName(TextLocation location, string name, bool isConstant) {
        var variableWord = isConstant ? "constant" : "variable";
        var message = $"{variableWord} name '{name}' is not valid as it is the name of a type in this namespace";
        return CreateError(DiagnosticCode.ERR_VariableUsingTypeName, location, message);
    }

    internal static BelteDiagnostic CannotImplyNull(TextLocation location) {
        var message = "cannot implicitly pass null in a non-nullable context";
        return CreateError(DiagnosticCode.ERR_CannotImplyNull, location, message);
    }

    internal static BelteDiagnostic CannotConvertNull(TextLocation location, TypeSymbol to, int argument = 0) {
        var message = $"cannot convert 'null' to '{to}' because it is a non-nullable type";

        if (argument > 0)
            message = $"argument {argument}: " + message;

        return CreateError(DiagnosticCode.ERR_CannotConvertNull, location, message);
    }

    internal static BelteDiagnostic ModifierAlreadyApplied(TextLocation location, SyntaxToken modifier) {
        var message = $"modifier '{modifier.text}' has already been applied to this item";
        return CreateError(DiagnosticCode.ERR_ModifierAlreadyApplied, location, message);
    }

    internal static Diagnostic CannotUseRef() {
        var message = "cannot use a reference type in this context";
        return CreateError(DiagnosticCode.ERR_CannotUseRef, message);
    }

    internal static BelteDiagnostic DivideByZero(TextLocation location) {
        var message = "cannot divide by zero";
        return CreateError(DiagnosticCode.ERR_DivideByZero, location, message);
    }

    internal static BelteDiagnostic NameUsedInEnclosingScope(TextLocation location, string name) {
        var message = $"a local named '{name}' cannot be declared in this scope because that name is used " +
            "in an enclosing scope to define a local or parameter";
        return CreateError(DiagnosticCode.ERR_NameUsedInEnclosingScope, location, message);
    }

    internal static BelteDiagnostic NullInitializerListOnImplicit(TextLocation location, bool isConstant) {
        var variableWord = isConstant ? "constant" : "variable";
        var message = $"cannot initialize an implicitly-typed {variableWord} with an " +
            "initializer list only containing 'null'";
        return CreateError(DiagnosticCode.ERR_NullInitializerListOnImplicit, location, message);
    }

    internal static Diagnostic UnrecognizedEscapeSequence(char escapeChar) {
        var message = $"unrecognized escape sequence '\\{escapeChar}'";
        return CreateError(DiagnosticCode.ERR_UnrecognizedEscapeSequence, message);
    }

    internal static BelteDiagnostic PrimitivesDoNotHaveMembers(TextLocation location) {
        var message = "primitive types do not contain any members";
        return CreateError(DiagnosticCode.ERR_PrimitivesDoNotHaveMembers, location, message);
    }

    internal static BelteDiagnostic CannotConstructPrimitive(TextLocation location, string name) {
        var message = $"type '{name}' is a primitive; primitives cannot be created with constructors";
        return CreateError(DiagnosticCode.ERR_CannotConstructPrimitive, location, message);
    }

    internal static BelteDiagnostic NoTemplateOverload(TextLocation location, string name) {
        var message = $"no overload for template '{name}' matches template argument list";
        return CreateError(DiagnosticCode.ERR_NoTemplateOverload, location, message);
    }

    internal static BelteDiagnostic AmbiguousTemplateOverload(TextLocation location, ISymbolWithTemplates[] symbols) {
        var message = new StringBuilder($"template is ambiguous between ");

        for (var i = 0; i < symbols.Length; i++) {
            if (i == symbols.Length - 1 && i > 1)
                message.Append(", and ");
            else if (i == symbols.Length - 1)
                message.Append(" and ");
            else if (i > 0)
                message.Append(", ");

            message.Append($"'{symbols[i]}'");
        }

        return CreateError(DiagnosticCode.ERR_AmbiguousTemplateOverload, location, message.ToString());
    }

    internal static BelteDiagnostic CannotUseStruct(TextLocation location) {
        var message = "cannot use structs outside of low-level contexts";
        return CreateError(DiagnosticCode.ERR_CannotUseStruct, location, message);
    }

    internal static BelteDiagnostic CannotUseThis(TextLocation location) {
        var message = "cannot use the 'this' keyword in the current context";
        return CreateError(DiagnosticCode.ERR_CannotUseThis, location, message);
    }

    internal static BelteDiagnostic MemberIsInaccessible(TextLocation location, Symbol symbol) {
        var message = $"'{symbol.ToDisplayString(SymbolDisplayFormat.QualifiedNameFormat)}' is inaccessible due to its protection level";
        return CreateError(DiagnosticCode.ERR_MemberIsInaccessible, location, message);
    }

    internal static BelteDiagnostic WrongConstructorArgumentCount(TextLocation location, string name, int argCount) {
        var message = $"type '{name}' does not contain a constructor that takes {argCount} arguments";
        return CreateError(DiagnosticCode.ERR_WrongConstructorArgumentCount, location, message);
    }

    internal static BelteDiagnostic InvalidModifier(TextLocation location, string name) {
        var message = $"modifier '{name}' is not valid for this item";
        return CreateError(DiagnosticCode.ERR_InvalidModifier, location, message);
    }

    internal static Diagnostic InvalidModifier(string name) {
        var message = $"modifier '{name}' is not valid for this item";
        return CreateError(DiagnosticCode.ERR_InvalidModifier, message);
    }

    internal static BelteDiagnostic NoInstanceRequired(TextLocation location, string name, Symbol symbol) {
        var message = $"member '{name}' cannot be accessed with an instance reference; qualify it with the type name instead";
        var suggestion = $"{symbol}.{name}";
        return CreateError(DiagnosticCode.ERR_NoInstanceRequired, location, message, suggestion);
    }

    internal static BelteDiagnostic NoInstanceRequired(TextLocation location, Symbol symbol) {
        var message = $"member '{symbol}' cannot be accessed with an instance reference; qualify it with the type name instead";
        return CreateError(DiagnosticCode.ERR_NoInstanceRequired, location, message);
    }

    internal static BelteDiagnostic InstanceRequired(TextLocation location, Symbol symbol) {
        var message = $"an object reference is required for non-static member '{symbol}'";
        return CreateError(DiagnosticCode.ERR_InstanceRequired, location, message);
    }

    internal static Diagnostic CannotInitializeInStructs() {
        var message = "cannot initialize fields in structure definitions";
        return CreateError(DiagnosticCode.ERR_CannotInitializeInStructs, message);
    }

    internal static BelteDiagnostic MultipleMains(TextLocation location) {
        var message = "cannot have multiple 'Main' entry points";
        return CreateError(DiagnosticCode.ERR_MultipleMains, location, message);
    }

    internal static Diagnostic InvalidAttributes() {
        var message = "attributes are not valid in this context";
        return CreateError(DiagnosticCode.ERR_InvalidAttributes, message);
    }

    internal static BelteDiagnostic TemplateNotExpected(TextLocation location, string name) {
        var message = $"item '{name}' does not expect any template arguments";
        return CreateError(DiagnosticCode.ERR_TemplateNotExpected, location, message);
    }

    internal static BelteDiagnostic TemplateMustBeConstant(TextLocation location) {
        var message = "template argument must be a compile-time constant";
        return CreateError(DiagnosticCode.ERR_TemplateMustBeConstant, location, message);
    }

    internal static BelteDiagnostic CannotReferenceNonField(TextLocation location) {
        var message = "cannot reference non-field or non-variable item";
        return CreateError(DiagnosticCode.ERR_CannotReferenceNonField, location, message);
    }

    internal static BelteDiagnostic CannotUseType(TextLocation location, TypeSymbol type) {
        var message = $"'{type}' is a type, which is not valid in this context";
        return CreateError(DiagnosticCode.ERR_CannotUseType, location, message);
    }

    internal static BelteDiagnostic ConstructorInStaticClass(TextLocation location) {
        var message = $"static classes cannot have constructors";
        return CreateError(DiagnosticCode.ERR_ConstructorInStaticClass, location, message);
    }

    internal static BelteDiagnostic StaticDataContainer(TextLocation location) {
        var message = $"cannot declare a field or local with a static type";
        return CreateError(DiagnosticCode.ERR_StaticDataContainer, location, message);
    }

    internal static BelteDiagnostic CannotCreateStatic(TextLocation location, TypeSymbol type) {
        var message = $"cannot create an instance of the static class '{type}'";
        return CreateError(DiagnosticCode.ERR_CannotCreateStatic, location, message);
    }

    internal static BelteDiagnostic ConflictingModifiers(TextLocation location, string modifier1, string modifier2) {
        var message = $"cannot mark member as both {modifier1} and {modifier2}";
        return CreateError(DiagnosticCode.ERR_ConflictingModifiers, location, message);
    }

    internal static BelteDiagnostic AssignmentInConstMethod(TextLocation location) {
        var message = $"cannot assign to an instance member in a method marked as constant";
        return CreateError(DiagnosticCode.ERR_AssignmentInConstMethod, location, message);
    }

    internal static BelteDiagnostic NonConstantCallInConstant(TextLocation location, string signature) {
        var message = $"cannot call non-constant method '{signature}' in a method marked as constant";
        return CreateError(DiagnosticCode.ERR_NonConstantCallInConstant, location, message);
    }

    internal static BelteDiagnostic NonConstantCallOnConstant(TextLocation location, string signature) {
        var message = $"cannot call non-constant method '{signature}' on constant";
        return CreateError(DiagnosticCode.ERR_NonConstantCallOnConstant, location, message);
    }

    internal static BelteDiagnostic CannotBeRefAndConstexpr(TextLocation location) {
        var message = $"reference type cannot be marked as a constant expression because references are not compile-time constants";
        return CreateError(DiagnosticCode.ERR_CannotBeRefAndConstexpr, location, message);
    }

    internal static BelteDiagnostic NotConstantExpression(TextLocation location) {
        var message = $"expression is not a compile-time constant";
        return CreateError(DiagnosticCode.ERR_NotConstantExpression, location, message);
    }

    internal static BelteDiagnostic CannotReturnStatic(TextLocation location) {
        var message = $"static types cannot be used as return types";
        return CreateError(DiagnosticCode.ERR_CannotReturnStatic, location, message);
    }

    internal static BelteDiagnostic IncorrectOperatorParameterCount(
        TextLocation location,
        string @operator,
        int expectedArity) {
        var message = $"overloaded operator '{@operator}' takes {expectedArity} parameter{(expectedArity == 1 ? "" : "s")}";
        return CreateError(DiagnosticCode.ERR_IncorrectOperatorParameterCount, location, message);
    }

    internal static BelteDiagnostic OperatorMustBePublicAndStatic(TextLocation location) {
        var message = $"overloaded operators must be marked as public and static";
        return CreateError(DiagnosticCode.ERR_OperatorMustBePublicAndStatic, location, message);
    }

    internal static BelteDiagnostic OperatorInStaticClass(TextLocation location) {
        var message = $"static classes cannot contain operators";
        return CreateError(DiagnosticCode.ERR_OperatorInStaticClass, location, message);
    }

    internal static BelteDiagnostic OperatorAtLeastOneClassParameter(TextLocation location) {
        var message = $"at least one of the parameters of an operator must be the containing type";
        return CreateError(DiagnosticCode.ERR_OperatorAtLeastOneClassParameter, location, message);
    }

    internal static BelteDiagnostic OperatorMustReturnClass(TextLocation location) {
        var message = $"the return type for the '++' or '--' operator must be the containing type";
        return CreateError(DiagnosticCode.ERR_OperatorMustReturnClass, location, message);
    }

    internal static BelteDiagnostic IndexOperatorFirstParameter(TextLocation location) {
        var message = $"the first parameter for the '[]' operator must be the containing type";
        return CreateError(DiagnosticCode.ERR_IndexOperatorFirstParameter, location, message);
    }

    internal static BelteDiagnostic ArrayOutsideOfLowLevelContext(TextLocation location) {
        var message = $"cannot use arrays outside of low-level contexts";
        return CreateError(DiagnosticCode.ERR_ArrayOutsideOfLowLevelContext, location, message);
    }

    internal static Diagnostic EmptyCharacterLiteral() {
        var message = $"character literal cannot be empty";
        return CreateError(DiagnosticCode.ERR_EmptyCharacterLiteral, message);
    }

    internal static Diagnostic CharacterLiteralTooLong() {
        var message = $"character literal cannot be more than one character";
        return CreateError(DiagnosticCode.ERR_CharacterLiteralTooLong, message);
    }

    internal static BelteDiagnostic NoInitOnNonNullable(TextLocation location) {
        var message = $"non-nullable locals and class fields must have an initializer";
        return CreateError(DiagnosticCode.ERR_NoInitOnNonNullable, location, message);
    }

    internal static BelteDiagnostic CannotBePrivateAndVirtualOrAbstract(TextLocation location) {
        var message = $"virtual or abstract methods cannot be private";
        return CreateError(DiagnosticCode.ERR_CannotBePrivateAndVirtualOrAbstract, location, message);
    }

    internal static BelteDiagnostic NoSuitableOverrideTarget(TextLocation location) {
        var message = $"no suitable method found to override";
        return CreateError(DiagnosticCode.ERR_NoSuitableOverrideTarget, location, message);
    }

    internal static BelteDiagnostic OverrideCannotChangeAccessibility(
        TextLocation location,
        string oldAccessibility,
        string newAccessibility) {
        var message = $"cannot change access modifier of inherited member from '{oldAccessibility}' " +
            $"to '{newAccessibility}'; cannot change access modifiers when overriding inherited members";
        return CreateError(DiagnosticCode.ERR_OverrideCannotChangeAccessibility, location, message);
    }

    internal static BelteDiagnostic CannotDerivePrimitive(TextLocation location, TypeSymbol type) {
        var message = $"cannot derive from primitive type '{type}'";
        return CreateError(DiagnosticCode.ERR_CannotDerivePrimitive, location, message);
    }

    internal static BelteDiagnostic UnknownTemplate(TextLocation location, string typeName, string templateName) {
        var message = $"type '{typeName}' has no such template parameter '{templateName}'";
        return CreateError(DiagnosticCode.ERR_UnknownTemplate, location, message);
    }

    internal static BelteDiagnostic CannotExtendCheckNonType(TextLocation location, string name) {
        var message = $"template '{name}' is not a type; cannot extension check a non-type";
        return CreateError(DiagnosticCode.ERR_CannotExtendCheckNonType, location, message);
    }

    internal static BelteDiagnostic ConstraintIsNotConstant(TextLocation location) {
        var message = $"template constraint is not a compile-time constant";
        return CreateError(DiagnosticCode.ERR_ConstraintIsNotConstant, location, message);
    }

    internal static BelteDiagnostic StructTakesNoArguments(TextLocation location) {
        var message = $"struct constructors take no arguments";
        return CreateError(DiagnosticCode.ERR_StructTakesNoArguments, location, message);
    }

    internal static BelteDiagnostic ExtendConstraintFailed(
        TextLocation location,
        string constraint,
        int ordinal,
        string templateName,
        string extensionName) {
        var message = $"template constraint {ordinal} fails ('{constraint}'); '{templateName}' must be or inherit from '{extensionName}'";
        return CreateError(DiagnosticCode.ERR_ExtendConstraintFailed, location, message);
    }

    internal static BelteDiagnostic ConstraintWasNull(TextLocation location, string constraint, int ordinal) {
        var message = $"template constraint {ordinal} fails ('{constraint}'); constraint results in null";
        return CreateError(DiagnosticCode.ERR_ConstraintWasNull, location, message);
    }

    internal static BelteDiagnostic ConstraintFailed(TextLocation location, string constraint, int ordinal) {
        var message = $"template constraint {ordinal} fails ('{constraint}')";
        return CreateError(DiagnosticCode.ERR_ConstraintFailed, location, message);
    }

    internal static BelteDiagnostic CannotOverride(TextLocation location, string signature) {
        var message = $"cannot override inherited method '{signature}' because it is not marked virtual or override";
        return CreateError(DiagnosticCode.ERR_CannotOverride, location, message);
    }

    internal static BelteDiagnostic CannotUseGlobalInClass(TextLocation location, string name) {
        var message = $"cannot use global '{name}' in a class definition";
        return CreateError(DiagnosticCode.ERR_CannotUseGlobalInClass, location, message);
    }

    internal static BelteDiagnostic MemberShadowsParent(
        TextLocation location,
        string newSignature,
        string newTypeName,
        string parentSignature,
        string parentTypeName) {
        var message = $"'{newTypeName}.{newSignature}' hides inherited member '{parentTypeName}.{parentSignature}';" +
            " use the 'new' keyword if hiding was intended";
        return CreateError(DiagnosticCode.ERR_MemberShadowsParent, location, message);
    }

    internal static BelteDiagnostic ConflictingOverrideModifiers(TextLocation location) {
        var message = $"a member marked as override cannot be marked as new, abstract, or virtual";
        return CreateError(DiagnosticCode.ERR_ConflictingOverrideModifiers, location, message);
    }

    internal static BelteDiagnostic CannotDeriveSealed(TextLocation location, TypeSymbol type) {
        var message = $"cannot derive from sealed type '{type}'";
        return CreateError(DiagnosticCode.ERR_CannotDeriveSealed, location, message);
    }

    internal static BelteDiagnostic CannotDeriveStatic(TextLocation location, TypeSymbol type) {
        var message = $"cannot derive from static type '{type}'";
        return CreateError(DiagnosticCode.ERR_CannotDeriveStatic, location, message);
    }

    internal static BelteDiagnostic ExpectedType(TextLocation location) {
        var message = $"expected type";
        return CreateError(DiagnosticCode.ERR_ExpectedType, location, message);
    }

    internal static BelteDiagnostic CannotUseBase(TextLocation location) {
        var message = "cannot use the 'base' in the current context";
        return CreateError(DiagnosticCode.ERR_CannotUseBase, location, message);
    }

    internal static BelteDiagnostic CannotCreateAbstract(TextLocation location, TypeSymbol type) {
        var message = $"cannot create an instance of the abstract class '{type}'";
        return CreateError(DiagnosticCode.ERR_CannotCreateAbstract, location, message);
    }

    internal static BelteDiagnostic NonAbstractMustHaveBody(TextLocation location, MethodSymbol method) {
        var message = $"'{method}' must declare a body because it is not marked abstract";
        return CreateError(DiagnosticCode.ERR_NonAbstractMustHaveBody, location, message);
    }

    internal static BelteDiagnostic AbstractCannotHaveBody(TextLocation location, MethodSymbol method) {
        var message = $"'{method}' cannot declare a body because it is marked abstract";
        return CreateError(DiagnosticCode.ERR_AbstractCannotHaveBody, location, message);
    }

    internal static BelteDiagnostic AbstractMemberInNonAbstractType(TextLocation location, string name) {
        var message = $"'{name}' cannot be marked abstract because it is not contained by an abstract type";
        return CreateError(DiagnosticCode.ERR_AbstractMemberInNonAbstractType, location, message);
    }

    internal static BelteDiagnostic TypeDoesNotImplementAbstract(
        TextLocation location,
        string className,
        string signature,
        string containingTypeName) {
        var message = $"'{className}' must implement inherited abstract member '{containingTypeName}.{signature}'";
        return CreateError(DiagnosticCode.ERR_TypeDoesNotImplementAbstract, location, message);
    }

    internal static BelteDiagnostic MissingOperatorPair(
        TextLocation location,
        SyntaxKind existingOperator,
        SyntaxKind neededOperator) {
        var message = $"operator {DiagnosticText(existingOperator, false)} requires a matching operator " +
            $"{DiagnosticText(neededOperator, false)} to also be defined";
        return CreateError(DiagnosticCode.ERR_MissingOperatorPair, location, message);
    }

    internal static Diagnostic InvalidExpressionTerm(SyntaxKind kind) {
        var message = $"invalid expression term {kind}";
        return CreateError(DiagnosticCode.ERR_InvalidExpressionTerm, message);
    }

    internal static BelteDiagnostic MultipleAccessibilities(TextLocation location) {
        var message = $"cannot apply multiple accessibility modifiers";
        return CreateError(DiagnosticCode.ERR_MultipleAccessibilities, location, message);
    }

    internal static BelteDiagnostic CircularConstraint(TextLocation location, TemplateParameterSymbol templateParameter1, TemplateParameterSymbol templateParameter2) {
        var message = $"template parameters '{templateParameter1}' and '{templateParameter2}' form a circular constraint";
        return CreateError(DiagnosticCode.ERR_CircularConstraint, location, message);
    }

    internal static BelteDiagnostic TemplateObjectBaseWithPrimitiveBase(TextLocation location, TemplateParameterSymbol templateParameter1, TemplateParameterSymbol templateParameter2) {
        var message = $"template parameter '{templateParameter2}' cannot be used as a constraint for template parameter '{templateParameter1}'";
        return CreateError(DiagnosticCode.ERR_TemplateObjectBaseWithPrimitiveBase, location, message);
    }

    internal static BelteDiagnostic TemplateBaseConstraintConflict(TextLocation location, TemplateParameterSymbol templateParameter, TypeSymbol base1, TypeSymbol base2) {
        var message = $"template parameter '{templateParameter}' cannot be constrained to both types '{base1}' and '{base2}'";
        return CreateError(DiagnosticCode.ERR_TemplateBaseConstraintConflict, location, message);
    }

    internal static BelteDiagnostic TemplateBaseBothObjectAndPrimitive(TextLocation location, TemplateParameterSymbol templateParameter) {
        var message = $"template parameter '{templateParameter}' cannot be constrained as both an Object type and Primitive type";
        return CreateError(DiagnosticCode.ERR_TemplateBaseBothObjectAndPrimitive, location, message);
    }

    internal static BelteDiagnostic MemberNameSameAsType(TextLocation location, string name) {
        var message = $"cannot declare a member with the same name as the enclosing type '{name}'";
        return CreateError(DiagnosticCode.ERR_MemberNameSameAsType, location, message);
    }

    internal static BelteDiagnostic CircularBase(TextLocation location, Symbol type1, Symbol type2) {
        var message = $"circular base dependency involving '{type1}' and '{type2}'";
        return CreateError(DiagnosticCode.ERR_CircularBase, location, message);
    }

    internal static BelteDiagnostic InconsistentAccessibilityClass(TextLocation location, NamedTypeSymbol type1, NamedTypeSymbol type2) {
        var message = $"inconsistent accessibility: class '{type1}' is less accessible than class '{type2}'";
        return CreateError(DiagnosticCode.ERR_InconsistentAccessibilityClass, location, message);
    }

    internal static BelteDiagnostic StaticDeriveFromNotObject(TextLocation location, TypeSymbol type) {
        var message = $"cannot derive from type '{type}'; static classes must derive from Object";
        return CreateError(DiagnosticCode.ERR_StaticDeriveFromNotObject, location, message);
    }

    internal static BelteDiagnostic CannotDeriveTemplate(TextLocation location, TypeSymbol type) {
        var message = $"cannot derive from template parameter '{type}'";
        return CreateError(DiagnosticCode.ERR_CannotDeriveTemplate, location, message);
    }

    internal static BelteDiagnostic InconsistentAccessibilityField(TextLocation location, TypeSymbol type, FieldSymbol field) {
        var message = $"inconsistent accessibility: type '{type}' is less accessible than field '{field}'";
        return CreateError(DiagnosticCode.ERR_InconsistentAccessibilityField, location, message);
    }

    internal static BelteDiagnostic InconsistentAccessibilityOperatorReturn(TextLocation location, TypeSymbol type, MethodSymbol method) {
        var message = $"inconsistent accessibility: return type '{type}' is less accessible than operator '{method}'";
        return CreateError(DiagnosticCode.ERR_InconsistentAccessibilityOperatorReturn, location, message);
    }

    internal static BelteDiagnostic InconsistentAccessibilityReturn(TextLocation location, TypeSymbol type, MethodSymbol method) {
        var message = $"inconsistent accessibility: return type '{type}' is less accessible than method '{method}'";
        return CreateError(DiagnosticCode.ERR_InconsistentAccessibilityReturn, location, message);
    }

    internal static BelteDiagnostic InconsistentAccessibilityOperatorParameter(TextLocation location, TypeSymbol type, MethodSymbol method) {
        var message = $"inconsistent accessibility: parameter type '{type}' is less accessible than operator '{method}'";
        return CreateError(DiagnosticCode.ERR_InconsistentAccessibilityOperatorParameter, location, message);
    }

    internal static BelteDiagnostic InconsistentAccessibilityParameter(TextLocation location, TypeSymbol type, MethodSymbol method) {
        var message = $"inconsistent accessibility: parameter type '{type}' is less accessible than method '{method}'";
        return CreateError(DiagnosticCode.ERR_InconsistentAccessibilityParameter, location, message);
    }

    internal static Diagnostic NoSuitableEntryPoint() {
        var message = $"no suitable entry point found";
        return CreateError(DiagnosticCode.ERR_NoSuitableEntryPoint, message);
    }

    internal static BelteDiagnostic ArrayOfStaticType(TextLocation location, TypeSymbol type) {
        var message = $"array elements cannot be of static type {type}";
        return CreateError(DiagnosticCode.ERR_ArrayOfStaticType, location, message);
    }

    internal static BelteDiagnostic LocalUsedBeforeDeclarationAndHidesField(TextLocation location, DataContainerSymbol symbol, FieldSymbol field) {
        var message = $"cannot use local '{symbol}' before it is declared; '{symbol}' hides the field '{field}'";
        return CreateError(DiagnosticCode.ERR_LocalUsedBeforeDeclarationAndHidesField, location, message);
    }

    internal static BelteDiagnostic LocalUsedBeforeDeclaration(TextLocation location, DataContainerSymbol symbol) {
        var message = $"cannot use local '{symbol}' before it is declared";
        return CreateError(DiagnosticCode.ERR_LocalUsedBeforeDeclaration, location, message);
    }

    internal static BelteDiagnostic CannotUseThisInStaticMethod(TextLocation location) {
        var message = "cannot use the 'this' keyword in a static method";
        return CreateError(DiagnosticCode.ERR_CannotUseThisInStaticMethod, location, message);
    }

    internal static BelteDiagnostic CannotUseBaseInStaticMethod(TextLocation location) {
        var message = "cannot use the 'base' keyword in a static method";
        return CreateError(DiagnosticCode.ERR_CannotUseBaseInStaticMethod, location, message);
    }

    internal static BelteDiagnostic NoBaseClass(TextLocation location, Symbol symbol) {
        var message = $"cannot use the 'base' keyword; '{symbol}' has no base class";
        return CreateError(DiagnosticCode.ERR_CannotUseBaseInStaticMethod, location, message);
    }

    internal static BelteDiagnostic AmbiguousReference(TextLocation location, string name, Symbol first, Symbol second) {
        var message = $"'{name}' is an ambiguous reference between '{first}' and '{second}'";
        return CreateError(DiagnosticCode.ERR_AmbiguousReference, location, message);
    }

    internal static BelteDiagnostic AmbiguousMember(TextLocation location, Symbol first, Symbol second) {
        var message = $"Ambiguity between '{first}' and '{second}'";
        return CreateError(DiagnosticCode.ERR_AmbiguousMember, location, message);
    }

    internal static BelteDiagnostic InvalidProtectedAccess(TextLocation location, Symbol symbol, Symbol throughType, Symbol containingType) {
        var message = $"cannot access protected member '{symbol}' via a qualifier of type '{throughType}'; the qualifier must be of type '{containingType}' (or derive from it)";
        return CreateError(DiagnosticCode.ERR_InvalidProtectedAccess, location, message);
    }

    internal static BelteDiagnostic CannotInitializeVarWithStaticClass(TextLocation location, TypeSymbol type) {
        var message = $"cannot initialize an implicitly-typed data container with the static type '{type}'";
        return CreateError(DiagnosticCode.ERR_CannotInitializeVarWithStaticClass, location, message);
    }

    internal static BelteDiagnostic MustNotHaveRefReturn(TextLocation location) {
        var message = $"cannot return by-reference in a method without a reference return type";
        return CreateError(DiagnosticCode.ERR_MustNotHaveRefReturn, location, message);
    }

    internal static BelteDiagnostic MustHaveRefReturn(TextLocation location) {
        var message = $"must return by-reference in a method with a reference return type";
        return CreateError(DiagnosticCode.ERR_MustHaveRefReturn, location, message);
    }

    internal static BelteDiagnostic MethodGroupCannotBeUsedAsValue(TextLocation location, BoundMethodGroup methodGroup) {
        var message = $"method group '{methodGroup}' cannot be used as a value";
        return CreateError(DiagnosticCode.ERR_MethodGroupCannotBeUsedAsValue, location, message);
    }

    internal static BelteDiagnostic LocalShadowsParameter(TextLocation location, string name) {
        var message = $"cannot declare a local with the name '{name}' because that name is already used by a parameter in an enclosing scope";
        return CreateError(DiagnosticCode.ERR_LocalShadowsParameter, location, message);
    }

    internal static BelteDiagnostic ParameterOrLocalShadowsTemplateParameter(TextLocation location, string name) {
        var message = $"cannot declare a parameter, local, or local function with the name '{name}' because that name is already used by a template parameter in an enclosing scope";
        return CreateError(DiagnosticCode.ERR_ParameterOrLocalShadowsTemplateParameter, location, message);
    }

    internal static BelteDiagnostic LocalAlreadyDeclared(TextLocation location, string name) {
        var message = $"a local or local function with the name '{name}' has already been declared in this scope";
        return CreateError(DiagnosticCode.ERR_LocalAlreadyDeclared, location, message);
    }

    internal static BelteDiagnostic AmbiguousBinaryOperator(TextLocation location, string op, TypeSymbol left, TypeSymbol right) {
        var message = $"binary operator '{op}' is ambiguous for operands with types '{left.ToNullOrString()}' and '{right.ToNullOrString()}'";
        return CreateError(DiagnosticCode.ERR_AmbiguousBinaryOperator, location, message);
    }

    internal static BelteDiagnostic ProgramLocalReferencedOutsideOfTopLevelStatement(TextLocation location, SimpleNameSyntax node) {
        var message = $"cannot reference synthesized program local '{node}' outside of top level statements";
        return CreateError(DiagnosticCode.ERR_ProgramLocalReferencedOutsideOfTopLevelStatement, location, message);
    }

    internal static BelteDiagnostic ValueCannotBeNull(TextLocation location, TypeSymbol type) {
        var message = $"cannot convert null to '{type}' because it is a non-nullable type";
        return CreateError(DiagnosticCode.ERR_ValueCannotBeNull, location, message);
    }

    internal static BelteDiagnostic InvalidObjectCreation(TextLocation location) {
        var message = "invalid object creation";
        return CreateError(DiagnosticCode.ERR_InvalidObjectCreation, location, message);
    }

    internal static BelteDiagnostic AmbiguousUnaryOperator(TextLocation location, string op, TypeSymbol operandType) {
        var message = $"unary operator '{op}' is ambiguous for operands with type '{operandType.ToNullOrString()}'";
        return CreateError(DiagnosticCode.ERR_AmbiguousUnaryOperator, location, message);
    }

    internal static BelteDiagnostic RefConditionalNeedsTwoRefs(TextLocation location) {
        var message = $"both conditional operator operands must be ref expressions or neither may be a ref expressions";
        return CreateError(DiagnosticCode.ERR_RefConditionalNeedsTwoRefs, location, message);
    }

    internal static BelteDiagnostic NullAssertAlwaysThrows(TextLocation location) {
        var message = $"cannot perform a 'not null' assertion on an expression with constant value 'null'";
        return CreateError(DiagnosticCode.ERR_NullAssertAlwaysThrows, location, message);
    }

    internal static BelteDiagnostic NullAssertOnNonNullableType(TextLocation location, TypeSymbol type) {
        var message = $"cannot perform a 'not null' assertion on an expression with type '{type}' as it is a non-nullable type";
        return CreateError(DiagnosticCode.ERR_NullAssertOnNonNullableType, location, message);
    }

    internal static BelteDiagnostic CannotConvertToStatic(TextLocation location, TypeSymbol type) {
        var message = $"cannot cast to static type '{type}'";
        return CreateError(DiagnosticCode.ERR_CannotConvertToStatic, location, message);
    }

    internal static BelteDiagnostic ArraySizeInDeclaration(TextLocation location) {
        var message = $"cannot specify array size in a declaration";
        return CreateError(DiagnosticCode.ERR_ArraySizeInDeclaration, location, message);
    }

    internal static BelteDiagnostic ListNoTargetType(TextLocation location) {
        var message = $"there is no target type for the initializer list";
        return CreateError(DiagnosticCode.ERR_ListNoTargetType, location, message);
    }

    internal static BelteDiagnostic InstanceRequiredInFieldInitializer(TextLocation location, Symbol symbol) {
        var message = $"a field initializer cannot reference non-static member '{symbol}'";
        return CreateError(DiagnosticCode.ERR_InstanceRequiredInFieldInitializer, location, message);
    }

    internal static BelteDiagnostic ArgumentExtraRef(TextLocation location, string keyword, int arg) {
        var message = $"argument {arg} may not be passed with the '{keyword}' keyword";
        return CreateError(DiagnosticCode.ERR_ArgumentExtraRef, location, message);
    }

    internal static BelteDiagnostic ArgumentWrongRef(TextLocation location, string keyword, int arg) {
        var message = $"argument {arg} must be passed with the '{keyword}' keyword";
        return CreateError(DiagnosticCode.ERR_ArgumentWrongRef, location, message);
    }

    internal static BelteDiagnostic NoCorrespondingArgument(TextLocation location, string parameterName, Symbol member) {
        var message = $"there is no argument given that corresponds to the required parameter '{parameterName}' of '{member}'";
        return CreateError(DiagnosticCode.ERR_NoCorrespondingArgument, location, message);
    }

    internal static BelteDiagnostic BadNonTrailingNamedArgument(TextLocation location, string parameterName) {
        var message = $"named argument '{parameterName}' is used out-of-position but is followed by an unnamed argument";
        return CreateError(DiagnosticCode.ERR_BadNonTrailingNamedArgument, location, message);
    }

    internal static BelteDiagnostic NamedArgumentUsedInPositional(TextLocation location, string parameterName) {
        var message = $"named argument '{parameterName}' specifies a parameter for which a positional argument has already been given";
        return CreateError(DiagnosticCode.ERR_NamedArgumentUsedInPositional, location, message);
    }

    internal static BelteDiagnostic BadEmbeddedStatement(TextLocation location) {
        var message = $"embedded statement cannot be a declaration";
        return CreateError(DiagnosticCode.ERR_BadEmbeddedStatement, location, message);
    }

    internal static BelteDiagnostic IncrementableLValueExpected(TextLocation location) {
        var message = "left side of increment or decrement operation must be a variable, parameter, field, or indexer";
        return CreateError(DiagnosticCode.ERR_IncrementableLValueExpected, location, message);
    }

    internal static BelteDiagnostic RefLocalOrParameterExpected(TextLocation location) {
        var message = "left side of ref assignment must be a ref variable, ref field, ref parameter, or ref indexer";
        return CreateError(DiagnosticCode.ERR_RefLocalOrParameterExpected, location, message);
    }

    internal static BelteDiagnostic RefLValueExpected(TextLocation location) {
        var message = "ref value must be an assignable variable, field, parameter, or indexer";
        return CreateError(DiagnosticCode.ERR_RefLValueExpected, location, message);
    }

    internal static BelteDiagnostic RefReturnLValueExpected(TextLocation location) {
        var message = "an expression cannot be used in this context because it may not be passed or returned by reference";
        return CreateError(DiagnosticCode.ERR_RefReturnLValueExpected, location, message);
    }

    internal static BelteDiagnostic InternalError(TextLocation location) {
        var message = "internal non-fatal compiler error";
        return CreateError(DiagnosticCode.ERR_InternalError, location, message);
    }

    internal static BelteDiagnostic BadSKKnown(Symbol symbol, string kind1, string kind2) {
        var message = $"'{symbol}' is a {kind1} but is used like a {kind2}";
        return CreateError(DiagnosticCode.ERR_BadSKKnown, null, message);
    }

    internal static BelteDiagnostic BadSKKnown(TextLocation location, Symbol symbol, string kind1, string kind2) {
        var message = $"'{symbol}' is a {kind1} but is used like a {kind2}";
        return CreateError(DiagnosticCode.ERR_BadSKKnown, location, message);
    }

    internal static BelteDiagnostic NonInvocableMemberCalled(Symbol symbol) {
        var message = $"non-invocable member '{symbol}' cannot be used like a method";
        return CreateError(DiagnosticCode.ERR_NonInvocableMemberCalled, null, message);
    }

    internal static BelteDiagnostic BadSKUnknown(TextLocation location, Symbol symbol, string kind) {
        var message = $"'{symbol}' is a {kind}, which is not valid in the given context";
        return CreateError(DiagnosticCode.ERR_BadSKUnknown, location, message);
    }

    internal static BelteDiagnostic RefConstThis(TextLocation location) {
        var message = "cannot use 'this' as a ref value because it is constant";
        return CreateError(DiagnosticCode.ERR_RefConstLocal, location, message);
    }

    internal static BelteDiagnostic RefReturnThis(TextLocation location) {
        var message = "cannot return 'this' by reference";
        return CreateError(DiagnosticCode.ERR_RefReturnThis, location, message);
    }

    internal static BelteDiagnostic ConstantAssignmentThis(TextLocation location) {
        var message = "cannot assign to 'this' because it is constant";
        return CreateError(DiagnosticCode.ERR_ConstantAssignmentThis, location, message);
    }

    internal static BelteDiagnostic ReturnNotLValue(TextLocation location, MethodSymbol symbol) {
        var message = $"cannot modify the return value of '{symbol}' because it is not variable";
        return CreateError(DiagnosticCode.ERR_ReturnNotLValue, location, message);
    }

    internal static BelteDiagnostic RefReturnConstNotField(TextLocation location, string kind, Symbol symbol) {
        var message = $"cannot return {kind} '{symbol}' by writable reference because it is constant";
        return CreateError(DiagnosticCode.ERR_RefReturnConstNotField, location, message);
    }

    internal static BelteDiagnostic RefReturnConstNotField2(TextLocation location, string kind, Symbol symbol) {
        var message = $"members of {kind} '{symbol}' cannot be returned by writable reference because it is constant";
        return CreateError(DiagnosticCode.ERR_RefReturnConstNotField2, location, message);
    }

    internal static BelteDiagnostic RefConstNotField(TextLocation location, string kind, Symbol symbol) {
        var message = $"cannot use {kind} '{symbol}' as a ref value because it is constant";
        return CreateError(DiagnosticCode.ERR_RefConstNotField, location, message);
    }

    internal static BelteDiagnostic RefConstNotField2(TextLocation location, string kind, Symbol symbol) {
        var message = $"members of {kind} '{symbol}' cannot be used as a ref value because it is constant";
        return CreateError(DiagnosticCode.ERR_RefConstNotField2, location, message);
    }

    internal static BelteDiagnostic ConstantAssignmentNotField(TextLocation location, string kind, Symbol symbol) {
        var message = $"cannot assign to {kind} '{symbol}' or use it as the right hand side of a ref assignment because it is constant";
        return CreateError(DiagnosticCode.ERR_ConstantAssignmentNotField, location, message);
    }

    internal static BelteDiagnostic ConstantAssignmentNotField2(TextLocation location, string kind, Symbol symbol) {
        var message = $"cannot assign to a member of {kind} '{symbol}' or use it as the right hand side of a ref assignment because it is constant";
        return CreateError(DiagnosticCode.ERR_ConstantAssignmentNotField2, location, message);
    }

    internal static BelteDiagnostic RefReturnConstant(TextLocation location) {
        var message = "a constant field cannot be returned by writable reference";
        return CreateError(DiagnosticCode.ERR_RefReturnConstant, location, message);
    }

    internal static BelteDiagnostic RefConstant(TextLocation location) {
        var message = "a constant field cannot be used as a ref value (except in a constructor)";
        return CreateError(DiagnosticCode.ERR_RefConstant, location, message);
    }

    internal static BelteDiagnostic AssignmentConstantField(TextLocation location) {
        var message = "a constant field cannot be assigned to (except in a constructor)";
        return CreateError(DiagnosticCode.ERR_AssignmentConstantField, location, message);
    }

    internal static BelteDiagnostic RefReturnConstantStatic(TextLocation location) {
        var message = "a static constant field cannot be returned by writable reference";
        return CreateError(DiagnosticCode.ERR_RefReturnConstantStatic, location, message);
    }

    internal static BelteDiagnostic RefConstantStatic(TextLocation location) {
        var message = "a static constant field cannot be used as a ref value";
        return CreateError(DiagnosticCode.ERR_RefConstantStatic, location, message);
    }

    internal static BelteDiagnostic AssignmentConstantStatic(TextLocation location) {
        var message = "a static constant field cannot be assigned to";
        return CreateError(DiagnosticCode.ERR_AssignmentConstantStatic, location, message);
    }
    internal static BelteDiagnostic RefReturnConstant2(TextLocation location, Symbol field) {
        var message = $"members of constant field '{field}' cannot be returned by writable reference";
        return CreateError(DiagnosticCode.ERR_RefReturnConstant2, location, message);
    }

    internal static BelteDiagnostic RefConstant2(TextLocation location, Symbol field) {
        var message = $"members of constant field '{field}' cannot be used as a ref value (except in a constructor)";
        return CreateError(DiagnosticCode.ERR_RefConstant2, location, message);
    }

    internal static BelteDiagnostic AssignmentConstantField2(TextLocation location, Symbol field) {
        var message = $"members of constant field '{field}' cannot be modified (except in a constructor)";
        return CreateError(DiagnosticCode.ERR_AssignmentConstantField2, location, message);
    }

    internal static BelteDiagnostic RefReturnConstantStatic2(TextLocation location, Symbol field) {
        var message = $"fields of static constant field '{field}' cannot be returned by writable reference";
        return CreateError(DiagnosticCode.ERR_RefReturnConstantStatic2, location, message);
    }

    internal static BelteDiagnostic RefConstantStatic2(TextLocation location, Symbol field) {
        var message = $"fields of static constant field '{field}' cannot be used as a ref value";
        return CreateError(DiagnosticCode.ERR_RefConstantStatic2, location, message);
    }

    internal static BelteDiagnostic AssignmentConstantStatic2(TextLocation location, Symbol field) {
        var message = $"fields of static constant field '{field}' cannot be assigned to";
        return CreateError(DiagnosticCode.ERR_AssignmentConstantStatic2, location, message);
    }

    internal static BelteDiagnostic RefConstantLocalCause(TextLocation location, string name, string kind) {
        var message = $"cannot use '{name}' as a ref value because it is a {kind}";
        return CreateError(DiagnosticCode.ERR_RefConstantLocalCause, location, message);
    }

    internal static BelteDiagnostic AssignmentConstantLocalCause(TextLocation location, string name, string kind) {
        var message = $"cannot assign to '{name}' because it is a {kind}";
        return CreateError(DiagnosticCode.ERR_AssignmentConstantLocalCause, location, message);
    }

    internal static BelteDiagnostic PossibleBadNegativeCast(TextLocation location) {
        var message = $"to cast a negative value it must be enclosed in parentheses";
        return CreateError(DiagnosticCode.ERR_PossibleBadNegativeCast, location, message);
    }

    internal static BelteDiagnostic RefReturnMustHaveIdentityConversion(TextLocation location, TypeSymbol type) {
        var message = $"the return expression must be of type '{type}' because this method returns by reference";
        return CreateError(DiagnosticCode.ERR_RefReturnMustHaveIdentityConversion, location, message);
    }

    internal static BelteDiagnostic RefAssignmentMustHaveIdentityConversion(TextLocation location, TypeSymbol type) {
        var message = $"ehe expression must be of type '{type}' because it is being assigned by reference";
        return CreateError(DiagnosticCode.ERR_RefAssignmentMustHaveIdentityConversion, location, message);
    }

    internal static BelteDiagnostic LocalSameNameAsTemplate(TextLocation location, string name) {
        var message = $"'{name}': a parameter, local, or local function cannot have the same name as a method template parameter";
        return CreateError(DiagnosticCode.ERR_LocalSameNameAsTemplate, location, message);
    }

    internal static BelteDiagnostic DuplicateParameterName(TextLocation location, string name) {
        var message = $"the parameter name '{name}' is a duplicate";
        return CreateError(DiagnosticCode.ERR_DuplicateParameterName, location, message);
    }

    internal static BelteDiagnostic RecursiveConstructorCall(TextLocation location, MethodSymbol constructor) {
        var message = $"constructor '{constructor}' cannot call itself";
        return CreateError(DiagnosticCode.ERR_RecursiveConstructorCall, location, message);
    }

    internal static BelteDiagnostic NewTemplateWithArguments(TextLocation location, TemplateParameterSymbol symbol) {
        var message = $"'{symbol}': cannot provide arguments when creating an instance of a variable type";
        return CreateError(DiagnosticCode.ERR_NewTemplateWithArguments, location, message);
    }

    internal static BelteDiagnostic LookupInTemplateVariable(TextLocation location, TypeSymbol type) {
        var message = $"cannot do non-virtual member lookup in '{type}' because it is a template parameter";
        return CreateError(DiagnosticCode.ERR_LookupInTemplateVariable, location, message);
    }

    internal static BelteDiagnostic AbstractBaseCall(TextLocation location, Symbol symbol) {
        var message = $"cannot call abstract base member '{symbol}'";
        return CreateError(DiagnosticCode.ERR_AbstractBaseCall, location, message);
    }

    internal static BelteDiagnostic StaticMemberInObjectInitializer(TextLocation location, Symbol symbol) {
        var message = $"static field '{symbol}' cannot be assigned in an object initializer";
        return CreateError(DiagnosticCode.ERR_StaticMemberInObjectInitializer, location, message);
    }

    internal static BelteDiagnostic RefConditionalDifferentTypes(TextLocation location, TypeSymbol type) {
        var message = $"the expression must be of type '{type}' to match the alternative ref value";
        return CreateError(DiagnosticCode.ERR_RefConditionalDifferentTypes, location, message);
    }

    internal static BelteDiagnostic DuplicateTemplateParameter(TextLocation location, string name) {
        var message = $"duplicate template parameter '{name}'";
        return CreateError(DiagnosticCode.ERR_DuplicateTemplateParameter, location, message);
    }

    private static DiagnosticInfo ErrorInfo(DiagnosticCode code) {
        return new DiagnosticInfo((int)code, "BU", DiagnosticSeverity.Error);
    }

    private static Diagnostic CreateError(DiagnosticCode code, string message) {
        return new Diagnostic(ErrorInfo(code), message);
    }

    private static BelteDiagnostic CreateError(DiagnosticCode code, TextLocation location, string message) {
        return CreateError(code, location, message, []);
    }

    private static BelteDiagnostic CreateError(
        DiagnosticCode code,
        TextLocation location,
        string message,
        params string[] suggestions) {
        return new BelteDiagnostic(ErrorInfo(code), location, message, suggestions);
    }

    private static string DiagnosticText(SyntaxKind type, bool sayToken = true) {
        var factValue = SyntaxFacts.GetText(type);

        if (factValue is not null && type.IsToken() && sayToken)
            return $"token '{factValue}'";
        else if (factValue is not null)
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
