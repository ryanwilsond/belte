using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Syntax {

    internal abstract class Statement : Node { }

    internal sealed class VariableDeclaration : Statement {
        public override SyntaxType type => SyntaxType.VARIABLE_DECLARATION_STATEMENT;
        public Token keyword { get; }
        public Token id { get; }
        public Token equals { get; }
        public Expression init { get; }
        public Token semicolon { get; }

        public VariableDeclaration(Token keyword_, Token id_, Token equals_, Expression init_, Token semicolon_) {
            keyword = keyword_;
            id = id_;
            equals = equals_;
            init = init_;
            semicolon = semicolon_;
        }
    }

    internal sealed class BlockStatement : Statement {
        public Token lbrace { get; }
        public ImmutableArray<Statement> statements { get; }
        public Token rbrace { get; }
        public override SyntaxType type => SyntaxType.BLOCK_STATEMENT;

        public BlockStatement(Token lbrace_, ImmutableArray<Statement> statements_, Token rbrace_) {
            lbrace = lbrace_;
            statements = statements_;
            rbrace = rbrace_;
        }
    }

    internal sealed class ExpressionStatement : Statement {
        public Expression expr { get; }
        public Token semicolon { get; }
        public override SyntaxType type => SyntaxType.EXPRESSION_STATEMENT;

        public ExpressionStatement(Expression expr_, Token semicolon_) {
            expr = expr_;
            semicolon = semicolon_;
        }
    }

    internal sealed class IfStatement : Statement {
        public Token ifkeyword { get; }
        public Token lparen { get; }
        public Expression condition { get; }
        public Token rparen { get; }
        public Statement then { get; }
        public ElseClause elseclause { get; }
        public override SyntaxType type => SyntaxType.IF_STATEMENT;

        public IfStatement(Token ifkeyword_, Token lparen_, Expression condition_, Token rparen_, Statement then_, ElseClause elseclause_) {
            ifkeyword = ifkeyword_;
            lparen = lparen_;
            condition = condition_;
            rparen = rparen_;
            then = then_;
            elseclause = elseclause_;
        }
    }

    internal sealed class ElseClause : Node {
        public Token elsekeyword { get; }
        public Statement then { get; }
        public override SyntaxType type => SyntaxType.ELSE_CLAUSE;

        public ElseClause(Token elsekeyword_, Statement then_) {
            elsekeyword = elsekeyword_;
            then = then_;
        }
    }

    internal sealed class WhileStatement : Statement {
        public Token keyword { get; }
        public Token lparen { get; }
        public Expression condition { get; }
        public Token rparen { get; }
        public Statement body { get; }
        public override SyntaxType type => SyntaxType.WHILE_STATEMENT;

        public WhileStatement(Token keyword_, Token lparen_, Expression condition_, Token rparen_, Statement body_) {
            keyword = keyword_;
            lparen = lparen_;
            condition = condition_;
            rparen = rparen_;
            body = body_;
        }
    }

    internal sealed class ForStatement : Statement {
        public Token keyword { get; }
        public Token lparen { get; }
        public VariableDeclaration it { get; }
        public Expression condition { get; }
        public Token semicolon { get; }
        public AssignmentExpression step { get; }
        public Token rparen { get; }
        public Statement body { get; }
        public override SyntaxType type => SyntaxType.FOR_STATEMENT;

        public ForStatement(
            Token keyword_, Token lparen_, VariableDeclaration it_,
            Expression condition_, Token semicolon_, AssignmentExpression step_, Token rparen_, Statement body_) {
            keyword = keyword_;
            lparen = lparen_;
            it = it_;
            condition = condition_;
            semicolon = semicolon_;
            step = step_;
            rparen = rparen_;
            body = body_;
        }
    }
}
