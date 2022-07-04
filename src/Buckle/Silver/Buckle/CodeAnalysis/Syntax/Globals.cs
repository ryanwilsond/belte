
namespace Buckle.CodeAnalysis.Syntax;

internal abstract class Member : Node {
    protected Member(SyntaxTree syntaxTree) : base(syntaxTree) { }
}

internal sealed partial class GlobalStatement : Member {
    internal Statement statement { get; }
    internal override SyntaxType type => SyntaxType.GLOBAL_STATEMENT;

    internal GlobalStatement(SyntaxTree syntaxTree, Statement statement_) : base(syntaxTree) {
        statement = statement_;
    }
}
