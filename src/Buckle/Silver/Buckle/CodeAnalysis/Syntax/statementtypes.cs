using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Syntax {

    internal abstract class Statement : Node { }

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
}
