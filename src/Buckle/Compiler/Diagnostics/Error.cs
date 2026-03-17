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
        internal static BelteDiagnostic NonTypeTemplate(TextLocation location) {
            var message = "unsupported: cannot declare a non-type template when building for .NET, transpiling to C#, or executing";
            return CreateError(DiagnosticCode.UNS_NonTypeTemplate, location, message);
        }
    }

    internal static BelteDiagnostic InvalidReference(string reference) {
        var message = $"{reference}: no such file or invalid file type";
        return CreateError(DiagnosticCode.ERR_InvalidReference, null, message);
    }

    internal static Diagnostic InvalidType(string text, TypeSymbol type) {
        var message = $"'{text}' is not a valid '{type.ToDisplayString(SymbolDisplayFormat.QualifiedNameFormat)}'";
        return CreateError(DiagnosticCode.ERR_InvalidType, message);
    }

    internal static Diagnostic BadCharacter(char input) {
        var message = $"unexpected character '{input}'";
        return CreateError(DiagnosticCode.ERR_BadCharacter, message);
    }

    internal static Diagnostic UnexpectedToken(SyntaxKind unexpected) {
        var message = $"unexpected {DiagnosticText(unexpected)}";
        return CreateError(DiagnosticCode.ERR_UnexpectedToken, message);
    }

    internal static Diagnostic UnexpectedTokenExpectedAnother(SyntaxKind unexpected, SyntaxKind expected) {
        var message = $"unexpected {DiagnosticText(unexpected)}, expected {DiagnosticText(expected, false)}";
        return CreateError(DiagnosticCode.ERR_UnexpectedTokenExpectedAnother, message);
    }

    internal static Diagnostic ExpectedTokenAtEOF(SyntaxKind expected) {
        var message = $"expected {DiagnosticText(expected, false)} at end of input";
        return CreateError(DiagnosticCode.ERR_ExpectedTokenAtEOF, message);
    }

    internal static Diagnostic UnexpectedTokenExpectedOthers(SyntaxKind unexpected, SyntaxKind expected1, SyntaxKind expected2) {
        var message = $"unexpected {DiagnosticText(unexpected)}, expected {DiagnosticText(expected1, false)} or {DiagnosticText(expected2, false)}";
        return CreateError(DiagnosticCode.ERR_UnexpectedTokenExpectedOthers, message);
    }

    internal static Diagnostic ExpectedTokensAtEOF(SyntaxKind expected1, SyntaxKind expected2) {
        var message = $"expected {DiagnosticText(expected1, false)} or {DiagnosticText(expected2, false)} at end of input";
        return CreateError(DiagnosticCode.ERR_ExpectedTokensAtEOF, message);
    }

    internal static BelteDiagnostic UnexpectedToken(TextLocation location, SyntaxKind kind) {
        var message = $"unexpected {DiagnosticText(kind)}";
        return CreateError(DiagnosticCode.ERR_UnexpectedToken, location, message);
    }

    internal static BelteDiagnostic NoImplicitConversion(TextLocation location, TypeSymbol from, TypeSymbol to) {
        var message = $"cannot convert from type '{from.ToNullOrString()}' to '{to.ToNullOrString()}' implicitly";
        return CreateError(DiagnosticCode.ERR_NoImplicitConversion, location, message);
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
        var message = $"unary operator '{op}' is not defined for type '{operand.ToNullOrString(SymbolDisplayFormat.QualifiedNameFormat)}'";
        return CreateError(DiagnosticCode.ERR_InvalidUnaryOperatorUse, location, message);
    }

    internal static BelteDiagnostic NamedArgumentTwice(TextLocation location, string name) {
        var message = $"named argument '{name}' cannot be specified multiple times";
        return CreateError(DiagnosticCode.ERR_NamedArgumentTwice, location, message);
    }

    internal static BelteDiagnostic InvalidBinaryOperatorUse(TextLocation location, string op, TypeSymbol left, TypeSymbol right) {
        var message = $"binary operator '{op}' is not defined for operands of types '{left.ToNullOrString(SymbolDisplayFormat.QualifiedNameFormat)}' and '{right.ToNullOrString(SymbolDisplayFormat.QualifiedNameFormat)}'";
        return CreateError(DiagnosticCode.ERR_InvalidBinaryOperatorUse, location, message);
    }

    internal static BelteDiagnostic GlobalStatementsInMultipleFiles(TextLocation location) {
        var message = "multiple files with global statements creates ambiguous entry point";
        return CreateError(DiagnosticCode.ERR_GlobalStatementsInMultipleFiles, location, message);
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

    internal static Diagnostic AmbiguousElse() {
        var message = "ambiguous which if-statement this else-clause belongs to; use curly braces";
        return CreateError(DiagnosticCode.ERR_AmbiguousElse, message);
    }

    internal static BelteDiagnostic CannotApplyIndexing(TextLocation location, TypeSymbol type) {
        var message = $"cannot apply indexing with [] to an expression of type '{type.ToNullOrString()}'";
        return CreateError(DiagnosticCode.ERR_CannotApplyIndexing, location, message);
    }

    internal static Diagnostic UnterminatedString() {
        var message = "unterminated string literal";
        return CreateError(DiagnosticCode.ERR_UnterminatedString, message);
    }

    internal static BelteDiagnostic CannotCallNonMethod(TextLocation location) {
        var message = $"called object is not a method";
        return CreateError(DiagnosticCode.ERR_CannotCallNonMethod, location, message);
    }
    internal static BelteDiagnostic InvalidExpressionStatement(TextLocation location) {
        var message = "only assignment, call, throw, and increment expressions can be used as a statement";
        return CreateError(DiagnosticCode.ERR_InvalidExpressionStatement, location, message);
    }

    internal static BelteDiagnostic InvalidBreakOrContinue(TextLocation location) {
        var message = $"break and continue statements can only be used within a loop";
        return CreateError(DiagnosticCode.ERR_InvalidBreakOrContinue, location, message);
    }

    internal static BelteDiagnostic UnexpectedReturnValue(TextLocation location) {
        var message = "cannot return a value in a method returning void";
        return CreateError(DiagnosticCode.ERR_UnexpectedReturnValue, location, message);
    }

    internal static BelteDiagnostic MissingReturnValue(TextLocation location) {
        var message = "cannot return without a value in a method returning non-void";
        return CreateError(DiagnosticCode.ERR_MissingReturnValue, location, message);
    }

    internal static BelteDiagnostic NoInitOnImplicit(TextLocation location) {
        var message = "implicitly-typed locals must have initializer";
        return CreateError(DiagnosticCode.ERR_NoInitOnImplicit, location, message);
    }

    internal static Diagnostic UnterminatedComment() {
        var message = "unterminated multi-line comment";
        return CreateError(DiagnosticCode.ERR_UnterminatedComment, message);
    }

    internal static BelteDiagnostic NullAssignOnImplicit(TextLocation location) {
        var message = $"cannot assign <null> to an implicitly-typed data container";
        return CreateError(DiagnosticCode.ERR_NullAssignOnImplicit, location, message);
    }

    internal static Diagnostic NoCatchOrFinally() {
        var message = "try statement must have a catch or finally";
        return CreateError(DiagnosticCode.ERR_NoCatchOrFinally, message);
    }

    internal static Diagnostic ExpectedOverloadableOperator() {
        var message = $"expected overloadable unary, arithmetic, equality, or comparison operator";
        return CreateError(DiagnosticCode.ERR_ExpectedOverloadableOperator, message);
    }

    internal static Diagnostic ExpectedOverloadableBinaryOperator() {
        var message = $"expected overloadable arithmetic, equality, or comparison operator";
        return CreateError(DiagnosticCode.ERR_ExpectedOverloadableBinaryOperator, message);
    }

    internal static Diagnostic ExpectedOverloadableUnaryOperator() {
        var message = $"expected overloadable unary operator";
        return CreateError(DiagnosticCode.ERR_ExpectedOverloadableUnaryOperator, message);
    }

    internal static BelteDiagnostic InitializeByReferenceWithByValue(TextLocation location) {
        var message = $"a by-reference data container must be initialized with a reference";
        return CreateError(DiagnosticCode.ERR_InitializeByReferenceWithByValue, location, message);
    }

    internal static BelteDiagnostic InitializeByValueWithByReference(TextLocation location) {
        var message = $"cannot initialize a by-value data container with a reference";
        return CreateError(DiagnosticCode.ERR_InitializeByValueWithByReference, location, message);
    }

    internal static BelteDiagnostic ReferenceToConstant(TextLocation location) {
        var message = $"cannot assign a reference to a constant to a by-reference data container expecting a reference to a data container";
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

    internal static BelteDiagnostic NoSuchMember(TextLocation location, TypeSymbol operand, string text) {
        var message = $"'{operand.ToDisplayString(SymbolDisplayFormat.QualifiedNameFormat)}' contains no such member '{text}'";
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

    internal static BelteDiagnostic ConstantToNonConstantReference(TextLocation location) {
        var message = $"cannot assign a reference to a data container to a by-reference data container expecting a reference to a constant";
        return CreateError(DiagnosticCode.ERR_ConstantToNonConstantReference, location, message);
    }

    internal static BelteDiagnostic ParameterAlreadySpecified(TextLocation location, string name) {
        var message = $"named argument '{name}' specifies a parameter for which a positional argument has already " +
            "been given";
        return CreateError(DiagnosticCode.ERR_ParameterAlreadySpecified, location, message);
    }

    internal static BelteDiagnostic DefaultMustBeConstant(TextLocation location, string name) {
        var message = $"default parameter value for '{name}' must be a compile-time constant";
        return CreateError(DiagnosticCode.ERR_DefaultMustBeConstant, location, message);
    }

    internal static BelteDiagnostic DefaultBeforeNoDefault(TextLocation location) {
        var message = "all optional parameters must be specified after any required parameters";
        return CreateError(DiagnosticCode.ERR_DefaultBeforeNoDefault, location, message);
    }

    internal static BelteDiagnostic ConstantAndVariable(TextLocation location) {
        var message = "cannot mark a data container as both constant and variable";
        return CreateError(DiagnosticCode.ERR_ConstantAndVariable, location, message);
    }

    internal static BelteDiagnostic CannotImplyNull(TextLocation location, int argument, TypeSymbol type) {
        var message = $"argument {argument}: cannot implicitly pass 'null' to a parameter of non-nullable type '{type}'";
        return CreateError(DiagnosticCode.ERR_CannotImplyNull, location, message);
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

    internal static Diagnostic UnrecognizedEscapeSequence(char escapeChar) {
        var message = $"unrecognized escape sequence '\\{escapeChar}'";
        return CreateError(DiagnosticCode.ERR_UnrecognizedEscapeSequence, message);
    }

    internal static BelteDiagnostic PrimitivesDoNotHaveMembers(TextLocation location) {
        var message = "primitive types do not contain any members";
        return CreateError(DiagnosticCode.ERR_PrimitivesDoNotHaveMembers, location, message);
    }

    internal static BelteDiagnostic CannotConstructPrimitive(TextLocation location) {
        var message = $"invalid object creation; cannot construct primitive";
        return CreateError(DiagnosticCode.ERR_CannotConstructPrimitive, location, message);
    }

    // TODO implement error
    internal static BelteDiagnostic NoTemplateOverload(TextLocation location, string name) {
        var message = $"no overload for template '{name}' matches template argument list";
        return CreateError(DiagnosticCode.ERR_NoTemplateOverload, location, message);
    }

    // TODO implement error
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
        var message = "cannot use 'this' in the current context";
        return CreateError(DiagnosticCode.ERR_CannotUseThis, location, message);
    }

    internal static BelteDiagnostic MemberIsInaccessible(TextLocation location, Symbol symbol) {
        var message = $"'{symbol}' is inaccessible due to its protection level";
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
        var suggestion = $"{symbol.ToDisplayString(SymbolDisplayFormat.QualifiedNameFormat)}.{name}";
        return CreateError(DiagnosticCode.ERR_NoInstanceRequired, location, message, suggestion);
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

    internal static BelteDiagnostic InvalidAttributes(TextLocation location) {
        var message = "attributes are not valid in this context";
        return CreateError(DiagnosticCode.ERR_InvalidAttributes, location, message);
    }

    // TODO implement error
    internal static BelteDiagnostic TemplateNotExpected(TextLocation location, string name) {
        var message = $"item '{name}' does not expect any template arguments";
        return CreateError(DiagnosticCode.ERR_TemplateNotExpected, location, message);
    }

    // TODO implement error
    internal static BelteDiagnostic TemplateMustBeConstant(TextLocation location) {
        var message = "template argument must be a compile-time constant";
        return CreateError(DiagnosticCode.ERR_TemplateMustBeConstant, location, message);
    }

    internal static BelteDiagnostic ConstructorInStaticClass(TextLocation location) {
        var message = $"static classes cannot have constructors";
        return CreateError(DiagnosticCode.ERR_ConstructorInStaticClass, location, message);
    }

    internal static BelteDiagnostic StaticDataContainer(TextLocation location) {
        var message = $"cannot declare a field or data container with a static type";
        return CreateError(DiagnosticCode.ERR_StaticDataContainer, location, message);
    }

    internal static BelteDiagnostic CannotCreateStatic(TextLocation location, TypeSymbol type) {
        var message = $"cannot create an instance of the static class '{type.ToDisplayString(SymbolDisplayFormat.QualifiedNameFormat)}'";
        return CreateError(DiagnosticCode.ERR_CannotCreateStatic, location, message);
    }

    internal static BelteDiagnostic ConflictingModifiers(TextLocation location, string modifier1, string modifier2) {
        var message = $"cannot mark symbol as both {modifier1} and {modifier2}";
        return CreateError(DiagnosticCode.ERR_ConflictingModifiers, location, message);
    }

    internal static BelteDiagnostic AssignmentInConstMethod(TextLocation location) {
        var message = $"cannot assign to an instance member in a method marked as constant";
        return CreateError(DiagnosticCode.ERR_AssignmentInConstMethod, location, message);
    }

    internal static BelteDiagnostic NonConstantCallInConstant(TextLocation location, MethodSymbol symbol) {
        var message = $"cannot call non-constant method '{symbol}' in a method marked as constant";
        return CreateError(DiagnosticCode.ERR_NonConstantCallInConstant, location, message);
    }

    internal static BelteDiagnostic NonConstantCallOnConstant(TextLocation location, MethodSymbol symbol) {
        var message = $"cannot call non-constant method '{symbol}' on constant";
        return CreateError(DiagnosticCode.ERR_NonConstantCallOnConstant, location, message);
    }

    internal static BelteDiagnostic CannotBeRefAndConstexpr(TextLocation location) {
        var message = $"reference type cannot be marked as a constant expression because references are not compile-time constants";
        return CreateError(DiagnosticCode.ERR_CannotBeRefAndConstexpr, location, message);
    }

    internal static BelteDiagnostic ConstantExpected(TextLocation location) {
        var message = $"expected a compile-time constant value";
        return CreateError(DiagnosticCode.ERR_ConstantExpected, location, message);
    }

    internal static BelteDiagnostic CannotReturnStatic(TextLocation location, TypeSymbol type) {
        var message = $"'{type.ToDisplayString(SymbolDisplayFormat.QualifiedNameFormat)}': static types cannot be used as return types";
        return CreateError(DiagnosticCode.ERR_CannotReturnStatic, location, message);
    }

    internal static Diagnostic IncorrectBinaryOperatorArgs(string @operator) {
        var message = $"overloaded binary operator '{@operator}' takes 2 parameters";
        return CreateError(DiagnosticCode.ERR_IncorrectBinaryOperatorArgs, message);
    }

    internal static Diagnostic IncorrectUnaryOperatorArgs(string @operator) {
        var message = $"overloaded unary operator '{@operator}' takes 1 parameter";
        return CreateError(DiagnosticCode.ERR_IncorrectUnaryOperatorArgs, message);
    }

    internal static BelteDiagnostic OperatorMustBePublicAndStatic(TextLocation location) {
        var message = $"overloaded operators must be marked as public and static";
        return CreateError(DiagnosticCode.ERR_OperatorMustBePublicAndStatic, location, message);
    }

    internal static BelteDiagnostic OperatorInStaticClass(TextLocation location) {
        var message = $"static classes cannot contain operators";
        return CreateError(DiagnosticCode.ERR_OperatorInStaticClass, location, message);
    }

    // TODO implement error
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

    internal static BelteDiagnostic CannotBePrivateAndVirtualOrAbstract(TextLocation location, Symbol symbol) {
        var message = $"'{symbol}': virtual or abstract methods cannot be private";
        return CreateError(DiagnosticCode.ERR_CannotBePrivateAndVirtualOrAbstract, location, message);
    }

    internal static BelteDiagnostic CannotDerivePrimitive(TextLocation location, TypeSymbol type) {
        var message = $"cannot derive from primitive type '{type.ToDisplayString(SymbolDisplayFormat.QualifiedNameFormat)}'";
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

    // TODO implement error
    internal static BelteDiagnostic ConstraintIsNotConstant(TextLocation location) {
        var message = $"template constraint is not a compile-time constant";
        return CreateError(DiagnosticCode.ERR_ConstraintIsNotConstant, location, message);
    }

    internal static BelteDiagnostic ExtendConstraintFailed(TextLocation location, Symbol constructed, string parameter, Symbol type, Symbol extend) {
        var message = $"the type '{type}' must be or derive from '{extend.ToDisplayString(SymbolDisplayFormat.QualifiedNameFormat)}' in order to use it as parameter '{parameter}' in the template type or method '{constructed.ToDisplayString(SymbolDisplayFormat.QualifiedNameFormat)}'";
        return CreateError(DiagnosticCode.ERR_ExtendConstraintFailed, location, message);
    }

    // TODO implement error
    internal static BelteDiagnostic ConstraintWasNull(TextLocation location, string constraint, int ordinal) {
        var message = $"template constraint {ordinal} fails ('{constraint}'); constraint results in null";
        return CreateError(DiagnosticCode.ERR_ConstraintWasNull, location, message);
    }

    // TODO implement error
    internal static BelteDiagnostic ConstraintFailed(TextLocation location, string constraint, int ordinal) {
        var message = $"template constraint {ordinal} fails ('{constraint}')";
        return CreateError(DiagnosticCode.ERR_ConstraintFailed, location, message);
    }

    internal static BelteDiagnostic ConflictingOverrideModifiers(TextLocation location, Symbol symbol) {
        var message = $"'{symbol}': a member marked as override cannot be marked as new or virtual";
        return CreateError(DiagnosticCode.ERR_ConflictingOverrideModifiers, location, message);
    }

    internal static BelteDiagnostic CannotDeriveSealed(TextLocation location, TypeSymbol type) {
        var message = $"cannot derive from sealed type '{type.ToDisplayString(SymbolDisplayFormat.QualifiedNameFormat)}'";
        return CreateError(DiagnosticCode.ERR_CannotDeriveSealed, location, message);
    }

    internal static BelteDiagnostic CannotDeriveStatic(TextLocation location, TypeSymbol type) {
        var message = $"cannot derive from static type '{type.ToDisplayString(SymbolDisplayFormat.QualifiedNameFormat)}'";
        return CreateError(DiagnosticCode.ERR_CannotDeriveStatic, location, message);
    }

    internal static BelteDiagnostic CannotUseBase(TextLocation location) {
        var message = "cannot use 'base' in the current context";
        return CreateError(DiagnosticCode.ERR_CannotUseBase, location, message);
    }

    internal static BelteDiagnostic CannotCreateAbstract(TextLocation location, TypeSymbol type) {
        var message = $"cannot create an instance of the abstract class '{type.ToDisplayString(SymbolDisplayFormat.QualifiedNameFormat)}'";
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

    internal static BelteDiagnostic AbstractInNonAbstractType(TextLocation location, Symbol symbol, TypeSymbol type) {
        var message = $"'{symbol}' is abstract but it is contained in non-abstract type '{type.ToDisplayString(SymbolDisplayFormat.QualifiedNameFormat)}'";
        return CreateError(DiagnosticCode.ERR_AbstractInNonAbstractType, location, message);
    }

    internal static BelteDiagnostic TypeDoesNotImplementAbstract(TextLocation location, Symbol symbol, Symbol member) {
        var message = $"'{symbol.ToDisplayString(SymbolDisplayFormat.QualifiedNameFormat)}' does not implement inherited abstract member '{member}'";
        return CreateError(DiagnosticCode.ERR_TypeDoesNotImplementAbstract, location, message);
    }

    internal static BelteDiagnostic OperatorNeedsMatch(TextLocation location, MethodSymbol existingOperator, string neededOperator) {
        var message = $"the operator {existingOperator} requires a matching operator '{neededOperator}' to also be defined";
        return CreateError(DiagnosticCode.ERR_OperatorNeedsMatch, location, message);
    }

    internal static Diagnostic InvalidExpressionTerm(SyntaxKind kind) {
        var message = $"invalid expression term {DiagnosticText(kind, false)}";
        return CreateError(DiagnosticCode.ERR_InvalidExpressionTerm, message);
    }

    internal static BelteDiagnostic MultipleAccessibilities(TextLocation location) {
        var message = $"cannot apply multiple accessibility modifiers";
        return CreateError(DiagnosticCode.ERR_MultipleAccessibilities, location, message);
    }

    internal static BelteDiagnostic CircularConstraint(TextLocation location, string parameter1, string parameter2) {
        var message = $"template parameters '{parameter1}' and '{parameter2}' form a circular constraint";
        return CreateError(DiagnosticCode.ERR_CircularConstraint, location, message);
    }

    internal static BelteDiagnostic TemplateObjectBaseWithPrimitiveBase(TextLocation location, string parameter1, string parameter2) {
        var message = $"template parameter '{parameter1}' cannot be used as a constraint for template parameter '{parameter2}'";
        return CreateError(DiagnosticCode.ERR_TemplateObjectBaseWithPrimitiveBase, location, message);
    }

    internal static BelteDiagnostic TemplateBaseConstraintConflict(TextLocation location, string parameter, TypeSymbol base1, TypeSymbol base2) {
        var message = $"template parameter '{parameter}' cannot be constrained to both types '{base1.ToDisplayString(SymbolDisplayFormat.QualifiedNameFormat)}' and '{base2.ToDisplayString(SymbolDisplayFormat.QualifiedNameFormat)}'";
        return CreateError(DiagnosticCode.ERR_TemplateBaseConstraintConflict, location, message);
    }

    internal static BelteDiagnostic TemplateBaseBothObjectAndPrimitive(TextLocation location, string parameter) {
        var message = $"template parameter '{parameter}' cannot be constrained as both an object type and a primitive type";
        return CreateError(DiagnosticCode.ERR_TemplateBaseBothObjectAndPrimitive, location, message);
    }

    internal static BelteDiagnostic MemberNameSameAsType(TextLocation location, string name) {
        var message = $"cannot declare a member with the same name as the enclosing type '{name}'";
        return CreateError(DiagnosticCode.ERR_MemberNameSameAsType, location, message);
    }

    internal static BelteDiagnostic CircularBase(TextLocation location, Symbol type1, Symbol type2) {
        var message = $"circular base dependency involving '{type1.name}' and '{type2.name}'";
        return CreateError(DiagnosticCode.ERR_CircularBase, location, message);
    }

    internal static BelteDiagnostic InconsistentAccessibilityClass(TextLocation location, NamedTypeSymbol type1, NamedTypeSymbol type2) {
        var message = $"inconsistent accessibility: class '{type1.name}' is less accessible than class '{type2.name}'";
        return CreateError(DiagnosticCode.ERR_InconsistentAccessibilityClass, location, message);
    }

    internal static BelteDiagnostic StaticDeriveFromNotObject(TextLocation location, TypeSymbol type, TypeSymbol baseType) {
        var message = $"static class '{type.name}' cannot derive from type '{baseType.ToDisplayString(SymbolDisplayFormat.QualifiedNameFormat)}'; static classes must derive from Object";
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
        var message = $"array elements cannot be of static type '{type.ToDisplayString(SymbolDisplayFormat.QualifiedNameFormat)}'";
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
        var message = "cannot use 'this' in a static method";
        return CreateError(DiagnosticCode.ERR_CannotUseThisInStaticMethod, location, message);
    }

    internal static BelteDiagnostic CannotUseBaseInStaticMethod(TextLocation location) {
        var message = "cannot use 'base' in a static method";
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
        var message = $"'{symbol.ToDisplayString(SymbolDisplayFormat.QualifiedNameFormat)}' is a {kind1} but is used like a {kind2}";
        return CreateError(DiagnosticCode.ERR_BadSKKnown, null, message);
    }

    internal static BelteDiagnostic BadSKKnown(TextLocation location, Symbol symbol, string kind1, string kind2) {
        var message = $"'{symbol.ToDisplayString(SymbolDisplayFormat.QualifiedNameFormat)}' is a {kind1} but is used like a {kind2}";
        return CreateError(DiagnosticCode.ERR_BadSKKnown, location, message);
    }

    internal static BelteDiagnostic BadSKKnown(TextLocation location, SyntaxNode node, string kind1, string kind2) {
        var message = $"'{node}' is a {kind1} but is used like a {kind2}";
        return CreateError(DiagnosticCode.ERR_BadSKKnown, location, message);
    }

    internal static BelteDiagnostic NonInvocableMemberCalled(Symbol symbol) {
        var message = $"non-invocable member '{symbol}' cannot be used like a method";
        return CreateError(DiagnosticCode.ERR_NonInvocableMemberCalled, null, message);
    }

    internal static BelteDiagnostic BadSKUnknown(TextLocation location, Symbol symbol, string kind) {
        var message = $"'{symbol.ToDisplayString(SymbolDisplayFormat.QualifiedNameFormat)}' is a {kind}, which is not valid in the given context";
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

    internal static BelteDiagnostic RefDefaultValue(TextLocation location) {
        var message = "a ref parameter cannot have a default value";
        return CreateError(DiagnosticCode.ERR_RefDefaultValue, location, message);
    }

    internal static BelteDiagnostic NoCastForDefaultParameter(TextLocation location, TypeSymbol type1, TypeSymbol type2) {
        var message = $"a value of type '{type1}' cannot be used as a default parameter because there are no casts to type '{type2}'";
        return CreateError(DiagnosticCode.ERR_NoCastForDefaultParameter, location, message);
    }

    internal static BelteDiagnostic NotNullRefDefaultParameter(TextLocation location, string name, TypeSymbol type) {
        var message = $"parameter '{name}' is of type '{type}'; a default parameter value of a reference type can only be initialized with 'null'";
        return CreateError(DiagnosticCode.ERR_NotNullRefDefaultParameter, location, message);
    }

    internal static BelteDiagnostic InvalidRefParameter(TextLocation location) {
        var message = $"'ref' is not valid in this context";
        return CreateError(DiagnosticCode.ERR_InvalidRefParameter, location, message);
    }

    internal static BelteDiagnostic RefConstWrongOrder(TextLocation location) {
        var message = "'const' modifier must be specified after 'ref'";
        return CreateError(DiagnosticCode.ERR_RefConstWrongOrder, location, message);
    }

    internal static BelteDiagnostic ParameterIsStatic(TextLocation location, TypeSymbol type) {
        var message = $"'{type}': static types cannot be used as parameters";
        return CreateError(DiagnosticCode.ERR_ParameterIsStatic, location, message);
    }

    internal static BelteDiagnostic CircularConstantValue(TextLocation location, Symbol symbol) {
        var message = $"the evaluation of the constant value for '{symbol}' involves a circular definition";
        return CreateError(DiagnosticCode.ERR_CircularConstantValue, location, message);
    }

    internal static BelteDiagnostic DuplicateNameInClass(TextLocation location, Symbol type, string name) {
        var message = $"the type '{type.ToDisplayString(SymbolDisplayFormat.QualifiedNameFormat)}' already contains a definition for '{name}'";
        return CreateError(DiagnosticCode.ERR_DuplicateNameInClass, location, message);
    }

    internal static BelteDiagnostic OverloadRefKind(TextLocation location, Symbol type, string methodKind, string refKind1, string refKind2) {
        var message = $"'{type}' cannot define an overloaded {methodKind} that differs only on parameter modifiers '{refKind1}' and '{refKind2}'";
        return CreateError(DiagnosticCode.ERR_OverloadRefKind, location, message);
    }

    internal static BelteDiagnostic ConstructorAlreadyExists(TextLocation location, Symbol type) {
        var message = $"type '{type.ToDisplayString(SymbolDisplayFormat.QualifiedNameFormat)}' already defines a constructor with the same parameter types";
        return CreateError(DiagnosticCode.ERR_ConstructorAlreadyExists, location, message);
    }

    internal static BelteDiagnostic MemberAlreadyExists(TextLocation location, Symbol type, string name) {
        var message = $"type '{type.ToDisplayString(SymbolDisplayFormat.QualifiedNameFormat)}' already defines a member called '{name}' with the same parameter types";
        return CreateError(DiagnosticCode.ERR_MemberAlreadyExists, location, message);
    }

    internal static BelteDiagnostic ProtectedInStatic(TextLocation location, Symbol symbol) {
        var message = $"'{symbol}': static classes cannot contain protected members";
        return CreateError(DiagnosticCode.ERR_ProtectedInStatic, location, message);
    }

    internal static BelteDiagnostic SealedNonOverride(TextLocation location, Symbol symbol) {
        var message = $"'{symbol}' cannot be sealed because it is not an override";
        return CreateError(DiagnosticCode.ERR_SealedNonOverride, location, message);
    }

    internal static BelteDiagnostic AbstractAndSealed(TextLocation location, Symbol symbol) {
        var message = $"'{symbol}' cannot be both abstract and sealed";
        return CreateError(DiagnosticCode.ERR_AbstractAndSealed, location, message);
    }

    internal static BelteDiagnostic AbstractAndVirtual(TextLocation location, string kind, Symbol symbol) {
        var message = $"the abstract {kind} '{symbol}' cannot be marked virtual";
        return CreateError(DiagnosticCode.ERR_AbstractAndVirtual, location, message);
    }

    internal static BelteDiagnostic StaticAndConst(TextLocation location, Symbol symbol) {
        var message = $"the static member '{symbol}' cannot be marked 'const'";
        return CreateError(DiagnosticCode.ERR_StaticAndConst, location, message);
    }

    internal static BelteDiagnostic VirtualInSealedType(TextLocation location, Symbol symbol, TypeSymbol type) {
        var message = $"'{symbol}' is a new virtual member in sealed type '{type.ToDisplayString(SymbolDisplayFormat.QualifiedNameFormat)}'";
        return CreateError(DiagnosticCode.ERR_VirtualInSealedType, location, message);
    }

    internal static BelteDiagnostic InstanceMemberInStatic(TextLocation location, Symbol symbol) {
        var message = $"'{symbol}': cannot declare instance members in a static class";
        return CreateError(DiagnosticCode.ERR_InstanceMemberInStatic, location, message);
    }

    internal static BelteDiagnostic HidingAbstractMember(TextLocation location, Symbol symbol, Symbol hiddenMember) {
        var message = $"'{symbol}' hides inherited abstract member '{hiddenMember}'";
        return CreateError(DiagnosticCode.ERR_HidingAbstractMember, location, message);
    }

    internal static BelteDiagnostic CantOverrideNonMethod(TextLocation location, Symbol symbol, Symbol hiddenMember) {
        var message = $"'{symbol}': cannot override because '{hiddenMember}' is not a method";
        return CreateError(DiagnosticCode.ERR_CantOverrideNonMethod, location, message);
    }

    internal static BelteDiagnostic OverrideNotExpected(TextLocation location, Symbol symbol) {
        var message = $"'{symbol}': no suitable method found to override";
        return CreateError(DiagnosticCode.ERR_OverrideNotExpected, location, message);
    }

    internal static BelteDiagnostic AmbiguousOverride(TextLocation location, Symbol symbol1, Symbol symbol2, TypeSymbol type) {
        var message = $"the inherited members '{symbol1}' and '{symbol2}' have the same signature in type '{type}', so they cannot be overridden";
        return CreateError(DiagnosticCode.ERR_AmbiguousOverride, location, message);
    }

    internal static BelteDiagnostic CantOverrideNonVirtual(TextLocation location, Symbol symbol, Symbol hiddenMember) {
        var message = $"'{symbol}': cannot override inherited member '{hiddenMember}' because it is not marked virtual, abstract, or override";
        return CreateError(DiagnosticCode.ERR_CantOverrideNonVirtual, location, message);
    }

    internal static BelteDiagnostic CantOverrideSealed(TextLocation location, Symbol symbol, Symbol hiddenMember) {
        var message = $"'{symbol}': cannot override inherited member '{hiddenMember}' because it is sealed";
        return CreateError(DiagnosticCode.ERR_CantOverrideSealed, location, message);
    }

    internal static BelteDiagnostic CantChangeAccessOnOverride(TextLocation location, Symbol symbol, string accessibility, Symbol hiddenMember) {
        var message = $"'{symbol}': cannot change access modifiers when overriding '{accessibility}' inherited member '{hiddenMember}'";
        return CreateError(DiagnosticCode.ERR_CantChangeAccessOnOverride, location, message);
    }

    internal static BelteDiagnostic CantChangeRefReturnOnOverride(TextLocation location, Symbol symbol, Symbol hiddenMember) {
        var message = $"'{symbol}' must match by reference return of overridden member '{hiddenMember}'";
        return CreateError(DiagnosticCode.ERR_CantChangeRefReturnOnOverride, location, message);
    }

    internal static BelteDiagnostic CantChangeReturnTypeOnOverride(TextLocation location, Symbol symbol, Symbol hiddenMember, TypeSymbol returnType) {
        var message = $"'{symbol}': return type must be '{returnType}' to match overridden member '{hiddenMember}'";
        return CreateError(DiagnosticCode.ERR_CantChangeReturnTypeOnOverride, location, message);
    }

    internal static BelteDiagnostic OperatorCantReturnVoid(TextLocation location) {
        var message = $"user-defined operators cannot return void";
        return CreateError(DiagnosticCode.ERR_OperatorCantReturnVoid, location, message);
    }

    internal static BelteDiagnostic BadUnaryOperatorSignature(TextLocation location) {
        var message = $"the parameter of a unary operator must be the containing type";
        return CreateError(DiagnosticCode.ERR_BadUnaryOperatorSignature, location, message);
    }

    internal static BelteDiagnostic BadAbstractUnaryOperatorSignature(TextLocation location) {
        var message = $"the parameter of a unary operator must be the containing type, or its type parameter constrained to it";
        return CreateError(DiagnosticCode.ERR_BadAbstractUnaryOperatorSignature, location, message);
    }

    internal static BelteDiagnostic BadShiftOperatorSignature(TextLocation location) {
        var message = $"the first operand of an overloaded shift operator must have the same type as the containing type";
        return CreateError(DiagnosticCode.ERR_BadShiftOperatorSignature, location, message);
    }

    internal static BelteDiagnostic BadAbstractShiftOperatorSignature(TextLocation location) {
        var message = $"the first operand of an overloaded shift operator must have the same type as the containing type or its type parameter constrained to it";
        return CreateError(DiagnosticCode.ERR_BadAbstractShiftOperatorSignature, location, message);
    }

    internal static BelteDiagnostic BadBinaryOperatorSignature(TextLocation location) {
        var message = $"one of the parameters of a binary operator must be the containing type";
        return CreateError(DiagnosticCode.ERR_BadBinaryOperatorSignature, location, message);
    }

    internal static BelteDiagnostic BadAbstractBinaryOperatorSignature(TextLocation location) {
        var message = $"one of the parameters of a binary operator must be the containing type, or its type parameter constrained to it";
        return CreateError(DiagnosticCode.ERR_BadAbstractBinaryOperatorSignature, location, message);
    }

    internal static BelteDiagnostic BadAbstractEqualityOperatorSignature(TextLocation location, TypeSymbol type) {
        var message = $"one of the parameters of an equality, or inequality operator declared in interface '{type}' must be a type parameter on '{type}' constrained to '{type}'";
        return CreateError(DiagnosticCode.ERR_BadAbstractEqualityOperatorSignature, location, message);
    }

    internal static BelteDiagnostic BadIncrementOperatorSignature(TextLocation location) {
        var message = $"the parameter type for ++ or -- operator must be the containing type";
        return CreateError(DiagnosticCode.ERR_BadIncrementOperatorSignature, location, message);
    }

    internal static BelteDiagnostic BadAbstractIncrementOperatorSignature(TextLocation location) {
        var message = $"the parameter type for ++ or -- operator must be the containing type, or its type parameter constrained to it";
        return CreateError(DiagnosticCode.ERR_BadAbstractIncrementOperatorSignature, location, message);
    }

    internal static BelteDiagnostic BadIncrementReturnType(TextLocation location) {
        var message = $"the return type for ++ or -- operator must match the parameter type or be derived from the parameter type";
        return CreateError(DiagnosticCode.ERR_BadIncrementReturnType, location, message);
    }

    internal static BelteDiagnostic BadAbstractIncrementReturnType(TextLocation location) {
        var message = $"the return type for ++ or -- operator must either match the parameter type, or be derived from the parameter type, or be the containing type's type parameter constrained to it unless the parameter type is a different type parameter";
        return CreateError(DiagnosticCode.ERR_BadAbstractIncrementReturnType, location, message);
    }

    internal static BelteDiagnostic BadIndexCount(TextLocation location, int rank) {
        var message = $"wrong number of indices inside []; expected {rank}";
        return CreateError(DiagnosticCode.ERR_BadIndexCount, location, message);
    }

    internal static BelteDiagnostic MultipleUpdates(TextLocation location) {
        var message = "cannot have multiple 'Update' graphics update points";
        return CreateError(DiagnosticCode.ERR_MultipleUpdates, location, message);
    }

    internal static BelteDiagnostic SeparateMainAndUpdate(TextLocation location) {
        var message = "the 'Main' entry point and 'Update' graphics update point must be declared in the same class";
        return CreateError(DiagnosticCode.ERR_SeparateMainAndUpdate, location, message);
    }

    internal static BelteDiagnostic FieldsCannotBeImplicitlyTyped(TextLocation location) {
        var message = "fields cannot be implicitly typed";
        return CreateError(DiagnosticCode.ERR_FieldsCannotBeImplicitlyTyped, location, message);
    }

    internal static BelteDiagnostic NonIntArraySize(TextLocation location) {
        var message = "array sizes must be of type 'int!'";
        return CreateError(DiagnosticCode.ERR_NonIntArraySize, location, message);
    }

    internal static BelteDiagnostic BadArity(TextLocation location, Symbol type, string text, int arity)
        => BadArity(location, type.ToDisplayString(SymbolDisplayFormat.QualifiedNameFormat), text, arity);

    internal static BelteDiagnostic BadArity(TextLocation location, string type, string text, int arity) {
        var message = $"the template {text} '{type}' requires {arity} template arguments";
        return CreateError(DiagnosticCode.ERR_BadArity, location, message);
    }

    internal static BelteDiagnostic ProtectedInStruct(TextLocation location, Symbol symbol) {
        var message = $"'{symbol.ToDisplayString(SymbolDisplayFormat.QualifiedNameFormat)}': protected member declared in struct";
        return CreateError(DiagnosticCode.ERR_ProtectedInStruct, location, message);
    }

    internal static BelteDiagnostic EscapeCall(TextLocation location, Symbol symbol, string name) {
        var message = $"use of result of '{symbol}' in this context may expose locals referenced by parameter '{name}' outside of their declaration scope";
        return CreateError(DiagnosticCode.ERR_EscapeCall, location, message);
    }

    internal static BelteDiagnostic EscapeCall2(TextLocation location, Symbol symbol, string name) {
        var message = $"use of member of result of '{symbol}' in this context may expose locals referenced by parameter '{name}' outside of their declaration scope";
        return CreateError(DiagnosticCode.ERR_EscapeCall2, location, message);
    }

    internal static BelteDiagnostic EscapeLocal(TextLocation location, Symbol symbol) {
        var message = $"use of local '{symbol}' in this context may expose referenced locals outside of their declaration scope";
        return CreateError(DiagnosticCode.ERR_EscapeLocal, location, message);
    }

    internal static BelteDiagnostic RefAssignReturnOnly(TextLocation location, object name, SyntaxNode syntax) {
        var message = $"cannot ref-assign '{syntax}' to '{name}' because '{syntax}' can only escape the current method through a return statement";
        return CreateError(DiagnosticCode.ERR_RefAssignReturnOnly, location, message);
    }

    internal static BelteDiagnostic RefAssignNarrower(TextLocation location, object name, SyntaxNode syntax) {
        var message = $"cannot ref-assign '{syntax}' to '{name}' because '{syntax}' has a narrower escape scope than '{name}'";
        return CreateError(DiagnosticCode.ERR_RefAssignNarrower, location, message);
    }

    internal static BelteDiagnostic RefAssignValEscapeWider(TextLocation location, object name, SyntaxNode syntax) {
        var message = $"cannot ref-assign '{syntax}' to '{name}' because '{syntax}' has a wider value escape scope than '{name}' allowing assignment through '{name}' of values with narrower escapes scopes than '{syntax}'";
        return CreateError(DiagnosticCode.ERR_RefAssignValEscapeWider, location, message);
    }

    internal static BelteDiagnostic CallArgMixing(TextLocation location, Symbol symbol, string name) {
        var message = $"this combination of arguments to '{symbol}' is disallowed because it may expose variables referenced by parameter '{name}' outside of their declaration scope";
        return CreateError(DiagnosticCode.ERR_CallArgMixing, location, message);
    }

    internal static BelteDiagnostic MismatchedRefEscapeInTernary(TextLocation location) {
        var message = $"branches of a ref conditional operator cannot refer to variables with incompatible declaration scopes";
        return CreateError(DiagnosticCode.ERR_MismatchedRefEscapeInTernary, location, message);
    }

    internal static BelteDiagnostic RefReturnLocal(TextLocation location, Symbol symbol) {
        var message = $"cannot return local '{symbol}' by reference because it is not a ref local";
        return CreateError(DiagnosticCode.ERR_RefReturnLocal, location, message);
    }

    internal static BelteDiagnostic RefReturnLocal2(TextLocation location, Symbol symbol) {
        var message = $"cannot return a member of local '{symbol}' by reference because it is not a ref local";
        return CreateError(DiagnosticCode.ERR_RefReturnLocal2, location, message);
    }

    internal static BelteDiagnostic RefReturnNonreturnableLocal(TextLocation location, Symbol symbol) {
        var message = $"cannot return local '{symbol}' by reference because it was initialized to a value that cannot be returned by reference";
        return CreateError(DiagnosticCode.ERR_RefReturnNonreturnableLocal, location, message);
    }

    internal static BelteDiagnostic RefReturnNonreturnableLocal2(TextLocation location, Symbol symbol) {
        var message = $"a member of '{symbol}' is returned by reference but was initialized to a value that cannot be returned by reference";
        return CreateError(DiagnosticCode.ERR_RefReturnNonreturnableLocal2, location, message);
    }

    internal static BelteDiagnostic EscapeOther(TextLocation location) {
        var message = $"expression cannot be used in this context because it may indirectly expose variables outside of their declaration scope";
        return CreateError(DiagnosticCode.ERR_EscapeOther, location, message);
    }

    internal static BelteDiagnostic RefReturnParameter(TextLocation location, string name) {
        var message = $"cannot return a parameter by reference '{name}' because it is not a ref parameter";
        return CreateError(DiagnosticCode.ERR_RefReturnParameter, location, message);
    }

    internal static BelteDiagnostic RefReturnParameter2(TextLocation location, string name) {
        var message = $"cannot returns by reference a member of parameter '{name}' because is not a ref parameter";
        return CreateError(DiagnosticCode.ERR_RefReturnParameter2, location, message);
    }

    internal static BelteDiagnostic RefReturnOnlyParameter(TextLocation location, string name) {
        var message = $"cannot return a parameter by reference '{name}' through a ref parameter; it can only be returned in a return statement";
        return CreateError(DiagnosticCode.ERR_RefReturnOnlyParameter, location, message);
    }

    internal static BelteDiagnostic RefReturnOnlyParameter2(TextLocation location, string name) {
        var message = $"cannot return by reference a member of parameter '{name}' through a ref parameter; it can only be returned in a return statement";
        return CreateError(DiagnosticCode.ERR_RefReturnOnlyParameter2, location, message);
    }

    internal static BelteDiagnostic RefReturnScopedParameter(TextLocation location, string name) {
        var message = $"cannot return a parameter by reference '{name}' because it is scoped to the current method";
        return CreateError(DiagnosticCode.ERR_RefReturnScopedParameter, location, message);
    }

    internal static BelteDiagnostic RefReturnScopedParameter2(TextLocation location, string name) {
        var message = $"cannot return by reference a member of parameter '{name}' because it is scoped to the current method";
        return CreateError(DiagnosticCode.ERR_RefReturnScopedParameter2, location, message);
    }

    internal static BelteDiagnostic UnexpectedTemplateName(TextLocation location) {
        var message = $"unexpected use of a templated name";
        return CreateError(DiagnosticCode.ERR_UnexpectedTemplateName, location, message);
    }

    internal static BelteDiagnostic UnexpectedAliasName(TextLocation location) {
        var message = $"unexpected use of an aliased name";
        return CreateError(DiagnosticCode.ERR_UnexpectedAliasName, location, message);
    }

    internal static Diagnostic UnexpectedAliasName() {
        var message = $"unexpected use of an aliased name";
        return CreateError(DiagnosticCode.ERR_UnexpectedAliasName, message);
    }

    internal static BelteDiagnostic ColonColonWithTypeAlias(TextLocation location, string name) {
        var message = $"cannot use alias '{name}' with '::' since the alias references a type; use '.' instead";
        return CreateError(DiagnosticCode.ERR_ColonColonWithTypeAlias, location, message, $"{name}.");
    }

    internal static BelteDiagnostic DuplicateNameInNamespace(TextLocation location, string name, NamespaceSymbol @namespace) {
        var message = $"the namespace '{@namespace}' already contains a definition for '{name}'";
        return CreateError(DiagnosticCode.ERR_DuplicateNameInNamespace, location, message);
    }

    internal static BelteDiagnostic NoNamespacePrivate(TextLocation location) {
        var message = $"members defined in a namespace cannot be explicitly declared as private or protected";
        return CreateError(DiagnosticCode.ERR_NoNamespacePrivate, location, message);
    }

    internal static BelteDiagnostic DuplicateAlias(TextLocation location, string name) {
        var message = $"the using alias '{name}' appeared previously in this namespace";
        return CreateError(DiagnosticCode.ERR_DuplicateAlias, location, message);
    }

    internal static BelteDiagnostic DuplicateWithGlobalUsing(TextLocation location, Symbol symbol) {
        var message = $"the using directive for '{symbol}' appeared previously as global using";
        return CreateError(DiagnosticCode.ERR_DuplicateWithGlobalUsing, location, message);
    }

    internal static BelteDiagnostic NoAliasHere(TextLocation location) {
        var message = $"a 'using static' directive cannot be used to declare an alias";
        return CreateError(DiagnosticCode.ERR_NoAliasHere, location, message);
    }

    internal static BelteDiagnostic BadUsingType(TextLocation location, NamespaceOrTypeSymbol symbol) {
        var message = $"a 'using static' directive can only be applied to types; '{symbol}' is a namespace not a type; consider a 'using namespace' directive instead";
        return CreateError(DiagnosticCode.ERR_BadUsingType, location, message);
    }

    internal static BelteDiagnostic DuplicateUsing(TextLocation location, Symbol symbol) {
        var message = $"the using directive for '{symbol}' appeared previously in this namespace";
        return CreateError(DiagnosticCode.ERR_DuplicateUsing, location, message);
    }

    internal static BelteDiagnostic BadUsingNamespace(TextLocation location, Symbol symbol) {
        var message = $"a 'using namespace' directive can only be applied to namespaces; '{symbol}' is a type not a namespace; consider a 'using static' directive instead";
        return CreateError(DiagnosticCode.ERR_BadUsingNamespace, location, message);
    }

    internal static BelteDiagnostic BadUsingStaticType(TextLocation location, string kind) {
        var message = $"'{kind}' type is not valid for 'using static'; only a class or namespace can be used";
        return CreateError(DiagnosticCode.ERR_BadUsingStaticType, location, message);
    }

    internal static BelteDiagnostic ArrayInitToNonArrayType(TextLocation location) {
        var message = $"can only use array initializer expressions to assign to array types; try using a new expression instead";
        return CreateError(DiagnosticCode.ERR_ArrayInitToNonArrayType, location, message);
    }

    internal static BelteDiagnostic ArrayInitExpected(TextLocation location) {
        var message = $"a nested initializer list is expected";
        return CreateError(DiagnosticCode.ERR_ArrayInitExpected, location, message);
    }

    internal static BelteDiagnostic ArrayInitWrongLength(TextLocation location, long length) {
        var message = $"an array initializer of length '{length}' is expected";
        return CreateError(DiagnosticCode.ERR_ArrayInitWrongLength, location, message);
    }

    internal static BelteDiagnostic IncompatibleEntryPointReturn(TextLocation location, Symbol symbol) {
        var message = $"entry point '{symbol}' must return void to maintain compatibility with .NET";
        return CreateError(DiagnosticCode.ERR_IncompatibleEntryPointReturn, location, message);
    }

    internal static BelteDiagnostic ImplicitlyTypedLocalAssignedBadValue(TextLocation location, TypeSymbol type) {
        var message = $"cannot assign {type} to an implicitly-typed local";
        return CreateError(DiagnosticCode.ERR_ImplicitlyTypedLocalAssignedBadValue, location, message);
    }

    internal static BelteDiagnostic AnnotationsDisallowedInObjectCreation(TextLocation location) {
        var message = $"cannot use a non-nullable annotation in object creation";
        return CreateError(DiagnosticCode.ERR_AnnotationsDisallowedInObjectCreation, location, message);
    }

    internal static BelteDiagnostic CannotAnnotateStruct(TextLocation location) {
        var message = $"cannot use a non-nullable annotation on a struct type";
        return CreateError(DiagnosticCode.ERR_CannotAnnotateStruct, location, message);
    }

    internal static BelteDiagnostic MissingArraySize(TextLocation location) {
        var message = $"array creation must have array size or array initializer";
        return CreateError(DiagnosticCode.ERR_MissingArraySize, location, message);
    }

    internal static BelteDiagnostic UnexpectedArrayInit(TextLocation location) {
        var message = $"initializer lists can only be used in a data container or field initializer; try using a new expression instead";
        return CreateError(DiagnosticCode.ERR_UnexpectedArrayInit, location, message);
    }

    internal static BelteDiagnostic ImplicitAssignedInitializerList(TextLocation location) {
        var message = $"cannot initialize an implicitly-typed data container with an initializer list";
        return CreateError(DiagnosticCode.ERR_ImplicitAssignedInitializerList, location, message);
    }

    internal static BelteDiagnostic GlobalUsingInNamespace(TextLocation location) {
        var message = $"cannot use a global using directive in a namespace declaration";
        return CreateError(DiagnosticCode.ERR_GlobalUsingInNamespace, location, message);
    }

    internal static BelteDiagnostic DottedTypeNamesNotFound(TextLocation location, string text, NamespaceOrTypeSymbol symbol) {
        var message = $"the type name '{text}' does not exist in the type '{symbol}'";
        return CreateError(DiagnosticCode.ERR_DottedTypeNamesNotFound, location, message);
    }

    internal static BelteDiagnostic AliasNotFound(TextLocation location, string text) {
        var message = $"alias '{text}' not found";
        return CreateError(DiagnosticCode.ERR_AliasNotFound, location, message);
    }

    internal static BelteDiagnostic SingleTypeNameNotFound(TextLocation location, string text) {
        var message = $"the type or namespace name '{text}' could not be found";
        return CreateError(DiagnosticCode.ERR_SingleTypeNameNotFound, location, message);
    }

    internal static BelteDiagnostic GlobalSingleTypeNameNotFound(TextLocation location, string text) {
        var message = $"the type or namespace name '{text}' could not be found in the global namespace";
        return CreateError(DiagnosticCode.ERR_GlobalSingleTypeNameNotFound, location, message);
    }

    internal static BelteDiagnostic DottedTypeNamesNotFoundInNamespace(TextLocation location, string text, object container) {
        var message = $"the type or namespace name '{text}' does not exist in the namespace '{container}'";
        return CreateError(DiagnosticCode.ERR_DottedTypeNamesNotFoundInNamespace, location, message);
    }

    internal static BelteDiagnostic ConflictingAliasAndMember(TextLocation location, string alias, NamespaceOrTypeSymbol container) {
        var message = $"namespace '{container}' contains a definition conflicting with alias '{alias}'";
        return CreateError(DiagnosticCode.ERR_ConflictingAliasAndMember, location, message);
    }

    internal static BelteDiagnostic UnexpectedUnboundTemplateName(TextLocation location) {
        var message = $"unexpected use of an unbound template name";
        return CreateError(DiagnosticCode.ERR_UnexpectedUnboundTemplateName, location, message);
    }

    internal static BelteDiagnostic HasNoTemplate(TextLocation location, Symbol symbol, string text) {
        var message = $"the non-template {text} '{symbol}' cannot be used with template arguments";
        return CreateError(DiagnosticCode.ERR_HasNoTemplate, location, message);
    }

    internal static BelteDiagnostic TemplateNotAllowed(TextLocation location, Symbol symbol, string text) {
        var message = $"the {text} '{symbol}' cannot be used with template arguments";
        return CreateError(DiagnosticCode.ERR_HasNoTemplate, location, message);
    }

    internal static BelteDiagnostic BadTemplateArgument(TextLocation location, Symbol symbol) {
        var message = $"the type '{symbol}' may not be used as a type argument";
        return CreateError(DiagnosticCode.ERR_BadTemplateArgument, location, message);
    }

    internal static BelteDiagnostic TemplateIsStatic(TextLocation location, Symbol symbol) {
        var message = $"'{symbol}': static types cannot be used as type arguments";
        return CreateError(DiagnosticCode.ERR_TemplateIsStatic, location, message);
    }

    internal static BelteDiagnostic ObjectConstraintFailed(TextLocation location, Symbol constructed, string parameter, TypeSymbol type) {
        var message = $"the type '{type}' must be an object type in order to use it as parameter '{parameter}' in the template type or method '{constructed.ToDisplayString(SymbolDisplayFormat.QualifiedNameFormat)}'";
        return CreateError(DiagnosticCode.ERR_ObjectConstraintFailed, location, message);
    }

    internal static BelteDiagnostic PrimitiveConstraintFailed(TextLocation location, Symbol constructed, string parameter, TypeSymbol type) {
        var message = $"the type '{type}' must be a primitive type in order to use it as parameter '{parameter}' in the template type or method '{constructed.ToDisplayString(SymbolDisplayFormat.QualifiedNameFormat)}'";
        return CreateError(DiagnosticCode.ERR_PrimitiveConstraintFailed, location, message);
    }

    internal static BelteDiagnostic NotNullableConstraintFailed(TextLocation location, Symbol constructed, string parameter, TypeSymbol type) {
        var message = $"the type '{type}' must be a non-nullable type in order to use it as parameter '{parameter}' in the template type or method '{constructed.ToDisplayString(SymbolDisplayFormat.QualifiedNameFormat)}'";
        return CreateError(DiagnosticCode.ERR_NotNullableConstraintFailed, location, message);
    }

    internal static BelteDiagnostic DuplicateConstraint(TextLocation location, string parameter) {
        var message = $"duplicate constraint on template parameter '{parameter}'";
        return CreateError(DiagnosticCode.ERR_DuplicateConstraint, location, message);
    }

    internal static BelteDiagnostic CannotIsCheckNonType(TextLocation location, string name) {
        var message = $"template '{name}' is not a type; cannot is check a non-type";
        return CreateError(DiagnosticCode.ERR_CannotIsCheckNonType, location, message);
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
