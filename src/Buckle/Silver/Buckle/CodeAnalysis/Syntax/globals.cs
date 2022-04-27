
namespace Buckle.CodeAnalysis.Syntax;

internal abstract class Member : Node {
    protected Member(SyntaxTree syntaxTree) : base(syntaxTree) { }
}

internal sealed partial class GlobalStatement : Member {
    public Statement statement { get; }
    public override SyntaxType type => SyntaxType.GLOBAL_STATEMENT;

    public GlobalStatement(SyntaxTree syntaxTree, Statement statement_) : base(syntaxTree) {
        statement = statement_;
    }
}
