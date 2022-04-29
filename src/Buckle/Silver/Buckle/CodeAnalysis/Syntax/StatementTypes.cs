using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Syntax;

internal abstract class Statement : Node {
    protected Statement(SyntaxTree syntaxTree): base(syntaxTree) { }
}

internal sealed partial class VariableDeclarationStatement : Statement {
    public override SyntaxType type => SyntaxType.VARIABLE_DECLARATION_STATEMENT;
    public Token typeName { get; }
    public Token? openBracket { get; }
    public Token? closeBracket { get; }
    public Token identifier { get; }
    public Token? equals { get; }
    public Expression? initializer { get; }
    public Token semicolon { get; }

    public VariableDeclarationStatement(
        SyntaxTree syntaxTree, Token typeName_, Token openBracket_, Token closeBracket_,
        Token identifier_, Token equals_, Expression initializer_, Token semicolon_)
        : base(syntaxTree) {
        typeName = typeName_;
        openBracket = openBracket_;
        closeBracket = closeBracket_;
        identifier = identifier_;
        equals = equals_;
        initializer = initializer_;
        semicolon = semicolon_;
    }
}

internal sealed partial class BlockStatement : Statement {
    public Token openBrace { get; }
    public ImmutableArray<Statement> statements { get; }
    public Token closeBrace { get; }
    public override SyntaxType type => SyntaxType.BLOCK;

    public BlockStatement(
        SyntaxTree syntaxTree, Token openBrace_, ImmutableArray<Statement> statements_, Token closeBrace_)
        : base(syntaxTree) {
        openBrace = openBrace_;
        statements = statements_;
        closeBrace = closeBrace_;
    }
}

internal sealed partial class ExpressionStatement : Statement {
    public Expression? expression { get; }
    public Token semicolon { get; }
    public override SyntaxType type => SyntaxType.EXPRESSION_STATEMENT;

    public ExpressionStatement(SyntaxTree syntaxTree, Expression expression_, Token semicolon_) : base(syntaxTree) {
        expression = expression_;
        semicolon = semicolon_;
    }
}

internal sealed partial class IfStatement : Statement {
    public Token ifKeyword { get; }
    public Token openParenthesis { get; }
    public Expression condition { get; }
    public Token closeParenthesis { get; }
    public Statement then { get; }
    public ElseClause? elseClause { get; }
    public override SyntaxType type => SyntaxType.IF_STATEMENT;

    public IfStatement(
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
    public Token elseKeyword { get; }
    public Statement then { get; }
    public override SyntaxType type => SyntaxType.ELSE_CLAUSE;

    public ElseClause(SyntaxTree syntaxTree, Token elseKeyword_, Statement then_) : base(syntaxTree) {
        elseKeyword = elseKeyword_;
        then = then_;
    }
}

internal sealed partial class WhileStatement : Statement {
    public Token keyword { get; }
    public Token openParenthesis { get; }
    public Expression condition { get; }
    public Token closeParenthesis { get; }
    public Statement body { get; }
    public override SyntaxType type => SyntaxType.WHILE_STATEMENT;

    public WhileStatement(
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
    public Token keyword { get; }
    public Token openParenthesis { get; }
    public Statement initializer { get; }
    public Expression condition { get; }
    public Token semicolon { get; }
    public Expression step { get; }
    public Token closeParenthesis { get; }
    public Statement body { get; }
    public override SyntaxType type => SyntaxType.FOR_STATEMENT;

    public ForStatement(
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
    public Token doKeyword { get; }
    public Statement body { get; }
    public Token whileKeyword { get; }
    public Token openParenthesis { get; }
    public Expression condition { get; }
    public Token closeParenthesis { get; }
    public Token semicolon { get; }
    public override SyntaxType type => SyntaxType.DO_WHILE_STATEMENT;

    public DoWhileStatement(
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
    public Token keyword { get; }
    public Token semicolon { get; }
    public override SyntaxType type => SyntaxType.CONTINUE_STATEMENT;

    public ContinueStatement(SyntaxTree syntaxTree, Token keyword_, Token semicolon_) : base(syntaxTree) {
        keyword = keyword_;
        semicolon = semicolon_;
    }
}

internal sealed partial class BreakStatement : Statement {
    public Token keyword { get; }
    public Token semicolon { get; }
    public override SyntaxType type => SyntaxType.BREAK_STATEMENT;

    public BreakStatement(SyntaxTree syntaxTree, Token keyword_, Token semicolon_) : base(syntaxTree) {
        keyword = keyword_;
        semicolon = semicolon_;
    }
}

internal sealed partial class ReturnStatement : Statement {
    public Token keyword { get; }
    public Expression? expression { get; }
    public Token semicolon { get; }
    public override SyntaxType type => SyntaxType.RETURN_STATEMENT;

    public ReturnStatement(SyntaxTree syntaxTree, Token keyword_, Expression expression_, Token semicolon_)
        : base(syntaxTree) {
        keyword = keyword_;
        expression = expression_;
        semicolon = semicolon_;
    }
}
