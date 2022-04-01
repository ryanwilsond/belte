using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Syntax {

    internal abstract class Statement : Node { }

    internal sealed class VariableDeclaration : Statement {
        public override SyntaxType type => SyntaxType.VARIABLE_DECLARATION_STATEMENT;
        public Token keyword { get; }
        public Token identifier { get; }
        public Token equals { get; }
        public Expression initializer { get; }
        public Token semicolon { get; }

        public VariableDeclaration(
            Token keyword_, Token identifier_, Token equals_, Expression initializer_, Token semicolon_) {
            keyword = keyword_;
            identifier = identifier_;
            equals = equals_;
            initializer = initializer_;
            semicolon = semicolon_;
        }
    }

    internal sealed class BlockStatement : Statement {
        public Token openBrace { get; }
        public ImmutableArray<Statement> statements { get; }
        public Token closeBrace { get; }
        public override SyntaxType type => SyntaxType.BLOCK_STATEMENT;

        public BlockStatement(Token openBrace_, ImmutableArray<Statement> statements_, Token closeBrace_) {
            openBrace = openBrace_;
            statements = statements_;
            closeBrace = closeBrace_;
        }
    }

    internal sealed class ExpressionStatement : Statement {
        public Expression expression { get; }
        public Token semicolon { get; }
        public override SyntaxType type => SyntaxType.EXPRESSION_STATEMENT;

        public ExpressionStatement(Expression expression_, Token semicolon_) {
            expression = expression_;
            semicolon = semicolon_;
        }
    }

    internal sealed class IfStatement : Statement {
        public Token ifKeyword { get; }
        public Token openParenthesis { get; }
        public Expression condition { get; }
        public Token closeParenthesis { get; }
        public Statement then { get; }
        public ElseClause elseClause { get; }
        public override SyntaxType type => SyntaxType.IF_STATEMENT;

        public IfStatement(Token ifKeyword_, Token openParenthesis_, Expression condition_,
            Token closeParenthesis_, Statement then_, ElseClause elseClause_) {
            ifKeyword = ifKeyword_;
            openParenthesis = openParenthesis_;
            condition = condition_;
            closeParenthesis = closeParenthesis_;
            then = then_;
            elseClause = elseClause_;
        }
    }

    internal sealed class ElseClause : Node {
        public Token elseKeyword { get; }
        public Statement then { get; }
        public override SyntaxType type => SyntaxType.ELSE_CLAUSE;

        public ElseClause(Token elseKeyword_, Statement then_) {
            elseKeyword = elseKeyword_;
            then = then_;
        }
    }

    internal sealed class WhileStatement : Statement {
        public Token keyword { get; }
        public Token openParenthesis { get; }
        public Expression condition { get; }
        public Token closeParenthesis { get; }
        public Statement body { get; }
        public override SyntaxType type => SyntaxType.WHILE_STATEMENT;

        public WhileStatement(
            Token keyword_, Token openParenthesis_, Expression condition_, Token closeParenthesis_, Statement body_) {
            keyword = keyword_;
            openParenthesis = openParenthesis_;
            condition = condition_;
            closeParenthesis = closeParenthesis_;
            body = body_;
        }
    }

    internal sealed class ForStatement : Statement {
        public Token keyword { get; }
        public Token openParenthesis { get; }
        public VariableDeclaration stepper { get; }
        public Expression condition { get; }
        public Token semicolon { get; }
        public AssignmentExpression step { get; }
        public Token closeParenthesis { get; }
        public Statement body { get; }
        public override SyntaxType type => SyntaxType.FOR_STATEMENT;

        public ForStatement(
            Token keyword_, Token openParenthesis_, VariableDeclaration stepper_, Expression condition_,
            Token semicolon_, AssignmentExpression step_, Token closeParenthesis_, Statement body_) {
            keyword = keyword_;
            openParenthesis = openParenthesis_;
            stepper = stepper_;
            condition = condition_;
            semicolon = semicolon_;
            step = step_;
            closeParenthesis = closeParenthesis_;
            body = body_;
        }
    }
}
