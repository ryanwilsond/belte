using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Syntax {

    internal abstract class Member : Node { }

    internal sealed class GlobalStatement : Member {
        public Statement statement { get; }
        public override SyntaxType type => SyntaxType.GLOBAL_STATEMENT;

        public GlobalStatement(Statement statement_) {
            statement = statement_;
        }
    }
}
