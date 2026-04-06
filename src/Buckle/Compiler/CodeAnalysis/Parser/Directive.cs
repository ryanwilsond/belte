
namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

internal class Directive {
    private readonly DirectiveTriviaSyntax _node;

    internal Directive(DirectiveTriviaSyntax node) {
        _node = node;
    }

    internal SyntaxKind kind => _node.kind;

    internal bool branchTaken {
        get {
            if (_node is BranchingDirectiveTriviaSyntax branching)
                return branching.branchTaken;

            return false;
        }
    }

    internal string GetIdentifier() {
        return _node.kind switch {
            SyntaxKind.DefineDirectiveTrivia => ((DefineDirectiveTriviaSyntax)_node).name.text,
            SyntaxKind.UndefDirectiveTrivia => ((UndefDirectiveTriviaSyntax)_node).name.text,
            _ => null,
        };
    }
}
