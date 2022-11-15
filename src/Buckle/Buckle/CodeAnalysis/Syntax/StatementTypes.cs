using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// A statement node, a line of code that is its own idea.
/// Statements either end with a closing curly brace or semicolon.
/// </summary>
internal abstract class Statement : Node {
    protected Statement(SyntaxTree syntaxTree): base(syntaxTree) { }
}

/// <summary>
/// Variable declaration, definition is optional.
/// E.g. int myVar = 3;
/// </summary>
internal sealed partial class VariableDeclarationStatement : Statement {
    /// <param name="typeClause">Type clause of the variable being declared</param>
    /// <param name="identifier">Name of the variable</param>
    /// <param name="equals">Equals token (optional)</param>
    /// <param name="initializer">Definition value (optional)</param>
    internal VariableDeclarationStatement(
        SyntaxTree syntaxTree, TypeClause typeClause,
        Token identifier, Token equals, Expression initializer, Token semicolon)
        : base(syntaxTree) {
        this.typeClause = typeClause;
        this.identifier = identifier;
        this.equals = equals;
        this.initializer = initializer;
        this.semicolon = semicolon;
    }

    /// <summary>
    /// Type clause of the variable being declared.
    /// </summary>
    internal TypeClause typeClause { get; }

    /// <summary>
    /// Name of the variable.
    /// </summary>
    internal Token identifier { get; }

    /// <summary>
    /// Equals token (optional).
    /// </summary>
    internal Token? equals { get; }

    /// <summary>
    /// Definition value (optional).
    /// </summary>
    internal Expression? initializer { get; }

    internal Token semicolon { get; }

    internal override SyntaxType type => SyntaxType.VARIABLE_DECLARATION_STATEMENT;
}

/// <summary>
/// Block statement, group of statements enclosed by curly braces.
/// The child statements have their own local scope.
/// E.g.
/// {
///     ... statements ...
/// }
/// </summary>
internal sealed partial class BlockStatement : Statement {
    /// <param name="statements">Child statements</param>
    internal BlockStatement(
        SyntaxTree syntaxTree, Token openBrace, ImmutableArray<Statement> statements, Token closeBrace)
        : base(syntaxTree) {
        this.openBrace = openBrace;
        this.statements = statements;
        this.closeBrace = closeBrace;
    }

    internal Token openBrace { get; }

    internal ImmutableArray<Statement> statements { get; }

    internal Token closeBrace { get; }

    internal override SyntaxType type => SyntaxType.BLOCK;
}

/// <summary>
/// Expression statement, a statement that contains a single expression and a semicolon.
/// E.g. 4 + 3;
/// </summary>
internal sealed partial class ExpressionStatement : Statement {
    internal ExpressionStatement(SyntaxTree syntaxTree, Expression expression, Token semicolon) : base(syntaxTree) {
        this.expression = expression;
        this.semicolon = semicolon;
    }

    internal Expression? expression { get; }

    internal Token semicolon { get; }

    internal override SyntaxType type => SyntaxType.EXPRESSION_STATEMENT;
}

/// <summary>
/// Try block statement, including an catch and finally clause.
/// Either the catch or finally can be omitted (not both).
/// The finally block triggers whether or not the catch block threw.
/// E.g.
/// try {
///     ... statements that may throw ...
/// } catch {
///     ... handler code ...
/// } finally {
///     ... closing up code ...
/// }
/// </summary>
internal sealed partial class TryStatement : Statement {
    internal TryStatement(
        SyntaxTree syntaxTree, Token tryKeyword, BlockStatement body,
        CatchClause catchClause, FinallyClause finallyClause)
        : base(syntaxTree) {
        this.tryKeyword = tryKeyword;
        this.body = body;
        this.catchClause = catchClause;
        this.finallyClause = finallyClause;
    }

    internal Token tryKeyword { get; }

    internal BlockStatement body { get; }

    internal CatchClause? catchClause { get; }

    internal FinallyClause? finallyClause { get; }

    internal override SyntaxType type => SyntaxType.TRY_STATEMENT;
}

/// <summary>
/// Catch clause. Only used with the try statement.
/// E.g. (see TryStatement)
/// ... catch { ... }
/// </summary>
internal sealed partial class CatchClause : Node {
    internal CatchClause(SyntaxTree syntaxTree, Token catchKeyword, BlockStatement body) : base(syntaxTree) {
        this.catchKeyword = catchKeyword;
        this.body = body;
    }

    internal Token catchKeyword { get; }

    internal BlockStatement body { get; }

    internal override SyntaxType type => SyntaxType.CATCH_CLAUSE;
}

/// <summary>
/// Finally clause. Only used with the try statement.
/// E.g. (see TryStatement)
/// ... finally { ... }
/// </summary>
internal sealed partial class FinallyClause : Node {
    internal FinallyClause(SyntaxTree syntaxTree, Token finallyKeyword, BlockStatement body) : base(syntaxTree) {
        this.finallyKeyword = finallyKeyword;
        this.body = body;
    }

    internal Token finallyKeyword { get; }

    internal BlockStatement body { get; }

    internal override SyntaxType type => SyntaxType.FINALLY_CLAUSE;
}

/// <summary>
/// If statement. Includes an optional else clause.
/// E.g.
/// if (condition) {
///     ... statements ...
/// } else {
///     ... statement ...
/// }
/// </summary>
internal sealed partial class IfStatement : Statement {
    /// <param name="condition">Condition expression, must be of type bool</param>
    /// <param name="elseClause">Else clause (optional)</param>
    internal IfStatement(
        SyntaxTree syntaxTree, Token ifKeyword, Token openParenthesis, Expression condition,
        Token closeParenthesis, Statement then, ElseClause elseClause)
        : base(syntaxTree) {
        this.ifKeyword = ifKeyword;
        this.openParenthesis = openParenthesis;
        this.condition = condition;
        this.closeParenthesis = closeParenthesis;
        this.then = then;
        this.elseClause = elseClause;
    }

    internal Token ifKeyword { get; }

    internal Token openParenthesis { get; }

    /// <summary>
    /// Condition expression, of type bool.
    /// </summary>
    internal Expression condition { get; }

    internal Token closeParenthesis { get; }

    internal Statement then { get; }

    /// <summary>
    /// Else clause (includes keyword and body).
    /// </summary>
    internal ElseClause? elseClause { get; }

    internal override SyntaxType type => SyntaxType.IF_STATEMENT;
}

/// <summary>
/// Else clause. Only used with if statement.
/// </summary>
internal sealed partial class ElseClause : Node {
    internal ElseClause(SyntaxTree syntaxTree, Token elseKeyword, Statement body) : base(syntaxTree) {
        this.elseKeyword = elseKeyword;
        this.body = body;
    }

    internal Token elseKeyword { get; }

    internal Statement body { get; }

    internal override SyntaxType type => SyntaxType.ELSE_CLAUSE;
}

/// <summary>
/// While statement.
/// E.g.
/// while (condition) {
///     ... statements ...
/// }
/// </summary>
internal sealed partial class WhileStatement : Statement {
    internal WhileStatement(
        SyntaxTree syntaxTree, Token keyword, Token openParenthesis,
        Expression condition, Token closeParenthesis, Statement body)
        : base(syntaxTree) {
        this.keyword = keyword;
        this.openParenthesis = openParenthesis;
        this.condition = condition;
        this.closeParenthesis = closeParenthesis;
        this.body = body;
    }

    internal Token keyword { get; }

    internal Token openParenthesis { get; }

    internal Expression condition { get; }

    internal Token closeParenthesis { get; }

    internal Statement body { get; }

    internal override SyntaxType type => SyntaxType.WHILE_STATEMENT;
}

/// <summary>
/// For statement, uses 3 part system, not for each.
/// E.g.
/// for (iterator declaration; condition; step) {
///     ... statements ...
/// }
/// </summary>
internal sealed partial class ForStatement : Statement {
    /// <param name="initializer">Declaration or name of variable used for stepping</param>
    internal ForStatement(
        SyntaxTree syntaxTree, Token keyword, Token openParenthesis, Statement initializer,
        Expression condition, Token semicolon, Expression step, Token closeParenthesis, Statement body)
        : base(syntaxTree) {
        this.keyword = keyword;
        this.openParenthesis = openParenthesis;
        this.initializer = initializer;
        this.condition = condition;
        this.semicolon = semicolon;
        this.step = step;
        this.closeParenthesis = closeParenthesis;
        this.body = body;
    }

    internal Token keyword { get; }

    internal Token openParenthesis { get; }

    /// <summary>
    /// Declaration or name of variable used for stepping.
    /// </summary>
    internal Statement initializer { get; }

    internal Expression condition { get; }

    internal Token semicolon { get; }

    internal Expression step { get; }

    internal Token closeParenthesis { get; }

    internal Statement body { get; }

    internal override SyntaxType type => SyntaxType.FOR_STATEMENT;
}

/// <summary>
/// Do while statement.
/// E.g.
/// do {
///     ... statements ...
/// } while (condition);
/// </summary>
internal sealed partial class DoWhileStatement : Statement {
    internal DoWhileStatement(
        SyntaxTree syntaxTree, Token doKeyword, Statement body, Token whileKeyword,
        Token openParenthesis, Expression condition, Token closeParenthesis, Token semicolon)
        : base(syntaxTree) {
        this.doKeyword = doKeyword;
        this.body = body;
        this.whileKeyword = whileKeyword;
        this.openParenthesis = openParenthesis;
        this.condition = condition;
        this.closeParenthesis = closeParenthesis;
        this.semicolon = semicolon;
    }

    internal Token doKeyword { get; }

    internal Statement body { get; }

    internal Token whileKeyword { get; }

    internal Token openParenthesis { get; }

    internal Expression condition { get; }

    internal Token closeParenthesis { get; }

    internal Token semicolon { get; }

    internal override SyntaxType type => SyntaxType.DO_WHILE_STATEMENT;
}

/// <summary>
/// Continue statement. Only used in while, do while, and for statements (loops).
/// E.g. continue;
/// </summary>
internal sealed partial class ContinueStatement : Statement {
    internal ContinueStatement(SyntaxTree syntaxTree, Token keyword, Token semicolon) : base(syntaxTree) {
        this.keyword = keyword;
        this.semicolon = semicolon;
    }

    internal Token keyword { get; }

    internal Token semicolon { get; }

    internal override SyntaxType type => SyntaxType.CONTINUE_STATEMENT;
}

/// <summary>
/// Break statement. Only used in while, do while, and for statements (loops).
/// E.g. break;
/// </summary>
internal sealed partial class BreakStatement : Statement {
    internal BreakStatement(SyntaxTree syntaxTree, Token keyword, Token semicolon) : base(syntaxTree) {
        this.keyword = keyword;
        this.semicolon = semicolon;
    }

    internal Token keyword { get; }

    internal Token semicolon { get; }

    internal override SyntaxType type => SyntaxType.BREAK_STATEMENT;
}

/// <summary>
/// Return statement. Only used in function bodies, or scopes that are inside a function body.
/// Have an optional return value.
/// E.g. return 3;
/// </summary>
internal sealed partial class ReturnStatement : Statement {
    /// <param name="expression">Return value (optional)</param>
    internal ReturnStatement(SyntaxTree syntaxTree, Token keyword, Expression expression, Token semicolon)
        : base(syntaxTree) {
        this.keyword = keyword;
        this.expression = expression;
        this.semicolon = semicolon;
    }

    internal Token keyword { get; }

    /// <summary>
    /// Return value (optional).
    /// </summary>
    internal Expression? expression { get; }

    internal Token semicolon { get; }

    internal override SyntaxType type => SyntaxType.RETURN_STATEMENT;
}

/// <summary>
/// Local function declaration, aka nested function declaration.
/// Syntactically identical to function declarations, but inside the scope of another function.
/// </summary>
internal sealed partial class LocalFunctionDeclaration : Statement {
    /// <param name="identifier">Name of the function</param>
    internal LocalFunctionDeclaration(
        SyntaxTree syntaxTree, TypeClause returnType, Token identifier, Token openParenthesis,
        SeparatedSyntaxList<Parameter> parameters, Token closeParenthesis, BlockStatement body)
        : base(syntaxTree) {
        this.returnType = returnType;
        this.identifier = identifier;
        this.openParenthesis = openParenthesis;
        this.parameters = parameters;
        this.closeParenthesis = closeParenthesis;
        this.body = body;
    }

    internal TypeClause returnType { get; }

    /// <summary>
    /// Name of the function.
    /// </summary>
    internal Token identifier { get; }

    internal Token openParenthesis { get; }

    internal SeparatedSyntaxList<Parameter> parameters { get; }

    internal Token closeParenthesis { get; }

    internal BlockStatement body { get; }

    internal override SyntaxType type => SyntaxType.LOCAL_FUNCTION_DECLARATION;
}
