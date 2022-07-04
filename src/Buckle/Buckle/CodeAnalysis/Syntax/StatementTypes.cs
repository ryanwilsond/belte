using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Syntax;

internal abstract class Statement : Node {
    protected Statement(SyntaxTree syntaxTree): base(syntaxTree) { }
}

internal sealed partial class VariableDeclarationStatement : Statement {
    internal override SyntaxType type => SyntaxType.VARIABLE_DECLARATION_STATEMENT;
    internal TypeClause typeClause { get; }
    internal Token identifier { get; }
    internal Token? equals { get; }
    internal Expression? initializer { get; }
    internal Token semicolon { get; }

    internal VariableDeclarationStatement(
        SyntaxTree syntaxTree, TypeClause typeClause_,
        Token identifier_, Token equals_, Expression initializer_, Token semicolon_)
        : base(syntaxTree) {
        typeClause = typeClause_;
        identifier = identifier_;
        equals = equals_;
        initializer = initializer_;
        semicolon = semicolon_;
    }
}

internal sealed partial class BlockStatement : Statement {
    internal Token openBrace { get; }
    internal ImmutableArray<Statement> statements { get; }
    internal Token closeBrace { get; }
    internal override SyntaxType type => SyntaxType.BLOCK;

    internal BlockStatement(
        SyntaxTree syntaxTree, Token openBrace_, ImmutableArray<Statement> statements_, Token closeBrace_)
        : base(syntaxTree) {
        openBrace = openBrace_;
        statements = statements_;
        closeBrace = closeBrace_;
    }
}

internal sealed partial class ExpressionStatement : Statement {
    internal Expression? expression { get; }
    internal Token semicolon { get; }
    internal override SyntaxType type => SyntaxType.EXPRESSION_STATEMENT;

    internal ExpressionStatement(SyntaxTree syntaxTree, Expression expression_, Token semicolon_) : base(syntaxTree) {
        expression = expression_;
        semicolon = semicolon_;
    }
}

internal sealed partial class TryStatement : Statement {
    internal Token tryKeyword { get; }
    internal BlockStatement body { get; }
    internal CatchClause? catchClause { get; }
    internal FinallyClause? finallyClause { get; }
    internal override SyntaxType type => SyntaxType.TRY_STATEMENT;

    internal TryStatement(
        SyntaxTree syntaxTree, Token tryKeyword_, BlockStatement body_,
        CatchClause catchClause_, FinallyClause finallyClause_)
        : base(syntaxTree) {
        tryKeyword = tryKeyword_;
        body = body_;
        catchClause = catchClause_;
        finallyClause = finallyClause_;
    }
}

internal sealed partial class CatchClause : Node {
    internal Token catchKeyword { get; }
    internal BlockStatement body { get; }
    internal override SyntaxType type => SyntaxType.CATCH_CLAUSE;

    internal CatchClause(SyntaxTree syntaxTree, Token catchKeyword_, BlockStatement body_) : base(syntaxTree) {
        catchKeyword = catchKeyword_;
        body = body_;
    }
}

internal sealed partial class FinallyClause : Node {
    internal Token finallyKeyword { get; }
    internal BlockStatement body { get; }
    internal override SyntaxType type => SyntaxType.FINALLY_CLAUSE;

    internal FinallyClause(SyntaxTree syntaxTree, Token finallyKeyword_, BlockStatement body_) : base(syntaxTree) {
        finallyKeyword = finallyKeyword_;
        body = body_;
    }
}

internal sealed partial class IfStatement : Statement {
    internal Token ifKeyword { get; }
    internal Token openParenthesis { get; }
    internal Expression condition { get; }
    internal Token closeParenthesis { get; }
    internal Statement then { get; }
    internal ElseClause? elseClause { get; }
    internal override SyntaxType type => SyntaxType.IF_STATEMENT;

    internal IfStatement(
        SyntaxTree syntaxTree, Token ifKeyword_, Token openParenthesis_, Expression condition_,
        Token closeParenthesis_, Statement then_, ElseClause elseClause_)
        : base(syntaxTree) {
        ifKeyword = ifKeyword_;
        openParenthesis = openParenthesis_;
        condition = condition_;
        closeParenthesis = closeParenthesis_;
        then = then_;
        elseClause = elseClause_;
    }
}

internal sealed partial class ElseClause : Node {
    internal Token elseKeyword { get; }
    internal Statement body { get; }
    internal override SyntaxType type => SyntaxType.ELSE_CLAUSE;

    internal ElseClause(SyntaxTree syntaxTree, Token elseKeyword_, Statement body_) : base(syntaxTree) {
        elseKeyword = elseKeyword_;
        body = body_;
    }
}

internal sealed partial class WhileStatement : Statement {
    internal Token keyword { get; }
    internal Token openParenthesis { get; }
    internal Expression condition { get; }
    internal Token closeParenthesis { get; }
    internal Statement body { get; }
    internal override SyntaxType type => SyntaxType.WHILE_STATEMENT;

    internal WhileStatement(
        SyntaxTree syntaxTree, Token keyword_, Token openParenthesis_,
        Expression condition_, Token closeParenthesis_, Statement body_)
        : base(syntaxTree) {
        keyword = keyword_;
        openParenthesis = openParenthesis_;
        condition = condition_;
        closeParenthesis = closeParenthesis_;
        body = body_;
    }
}

internal sealed partial class ForStatement : Statement {
    internal Token keyword { get; }
    internal Token openParenthesis { get; }
    internal Statement initializer { get; }
    internal Expression condition { get; }
    internal Token semicolon { get; }
    internal Expression step { get; }
    internal Token closeParenthesis { get; }
    internal Statement body { get; }
    internal override SyntaxType type => SyntaxType.FOR_STATEMENT;

    internal ForStatement(
        SyntaxTree syntaxTree, Token keyword_, Token openParenthesis_, Statement initializer_,
        Expression condition_, Token semicolon_, Expression step_, Token closeParenthesis_, Statement body_)
        : base(syntaxTree) {
        keyword = keyword_;
        openParenthesis = openParenthesis_;
        initializer = initializer_;
        condition = condition_;
        semicolon = semicolon_;
        step = step_;
        closeParenthesis = closeParenthesis_;
        body = body_;
    }
}

internal sealed partial class DoWhileStatement : Statement {
    internal Token doKeyword { get; }
    internal Statement body { get; }
    internal Token whileKeyword { get; }
    internal Token openParenthesis { get; }
    internal Expression condition { get; }
    internal Token closeParenthesis { get; }
    internal Token semicolon { get; }
    internal override SyntaxType type => SyntaxType.DO_WHILE_STATEMENT;

    internal DoWhileStatement(
        SyntaxTree syntaxTree, Token doKeyword_, Statement body_, Token whileKeyword_,
        Token openParenthesis_, Expression condition_, Token closeParenthesis_, Token semicolon_)
        : base(syntaxTree) {
        doKeyword = doKeyword_;
        body = body_;
        whileKeyword = whileKeyword_;
        openParenthesis = openParenthesis_;
        condition = condition_;
        closeParenthesis = closeParenthesis_;
        semicolon = semicolon_;
    }
}

internal sealed partial class ContinueStatement : Statement {
    internal Token keyword { get; }
    internal Token semicolon { get; }
    internal override SyntaxType type => SyntaxType.CONTINUE_STATEMENT;

    internal ContinueStatement(SyntaxTree syntaxTree, Token keyword_, Token semicolon_) : base(syntaxTree) {
        keyword = keyword_;
        semicolon = semicolon_;
    }
}

internal sealed partial class BreakStatement : Statement {
    internal Token keyword { get; }
    internal Token semicolon { get; }
    internal override SyntaxType type => SyntaxType.BREAK_STATEMENT;

    internal BreakStatement(SyntaxTree syntaxTree, Token keyword_, Token semicolon_) : base(syntaxTree) {
        keyword = keyword_;
        semicolon = semicolon_;
    }
}

internal sealed partial class ReturnStatement : Statement {
    internal Token keyword { get; }
    internal Expression? expression { get; }
    internal Token semicolon { get; }
    internal override SyntaxType type => SyntaxType.RETURN_STATEMENT;

    internal ReturnStatement(SyntaxTree syntaxTree, Token keyword_, Expression expression_, Token semicolon_)
        : base(syntaxTree) {
        keyword = keyword_;
        expression = expression_;
        semicolon = semicolon_;
    }
}

internal sealed partial class LocalFunctionDeclaration : Statement {
    internal TypeClause returnType { get; }
    internal Token identifier { get; }
    internal Token openParenthesis { get; }
    internal SeparatedSyntaxList<Parameter> parameters { get; }
    internal Token closeParenthesis { get; }
    internal BlockStatement body { get; }
    internal override SyntaxType type => SyntaxType.LOCAL_FUNCTION_DECLARATION;

    internal LocalFunctionDeclaration(
        SyntaxTree syntaxTree, TypeClause returnType_, Token identifier_, Token openParenthesis_,
        SeparatedSyntaxList<Parameter> parameters_, Token closeParenthesis_, BlockStatement body_)
        : base(syntaxTree) {
        returnType = returnType_;
        identifier = identifier_;
        openParenthesis = openParenthesis_;
        parameters = parameters_;
        closeParenthesis = closeParenthesis_;
        body = body_;
    }
}
