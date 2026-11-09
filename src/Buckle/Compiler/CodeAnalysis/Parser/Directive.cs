
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

    internal bool isActive => _node.isActive;

    internal string GetIdentifier() {
        return _node.kind switch {
            SyntaxKind.DefineDirectiveTrivia => ((DefineDirectiveTriviaSyntax)_node).name.text,
            SyntaxKind.UndefDirectiveTrivia => ((UndefDirectiveTriviaSyntax)_node).name.text,
            _ => null,
        };
    }

    internal bool IncrementallyEquivalent(Directive other) {
        if (kind != other.kind)
            return false;

        var isActive = this.isActive;
        var otherIsActive = other.isActive;

        if (!isActive && !otherIsActive)
            return true;

        if (isActive != otherIsActive)
            return false;

        switch (kind) {
            case SyntaxKind.DefineDirectiveTrivia:
            case SyntaxKind.UndefDirectiveTrivia:
                return GetIdentifier() == other.GetIdentifier();
            case SyntaxKind.IfDirectiveTrivia:
            case SyntaxKind.ElifDirectiveTrivia:
            case SyntaxKind.ElseDirectiveTrivia:
                return branchTaken == other.branchTaken;
            default:
                return true;
        }
    }
}
