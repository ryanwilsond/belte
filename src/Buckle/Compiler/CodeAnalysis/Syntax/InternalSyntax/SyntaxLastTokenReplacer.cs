
namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

internal class SyntaxLastTokenReplacer : SyntaxRewriter {
    private readonly SyntaxToken _oldToken;
    private readonly SyntaxToken _newToken;
    private int _count = 1;
    private bool _found;

    private SyntaxLastTokenReplacer(SyntaxToken oldToken, SyntaxToken newToken) {
        _oldToken = oldToken;
        _newToken = newToken;
    }

    internal static T Replace<T>(T root, SyntaxToken newToken) where T : BelteSyntaxNode {
        var oldToken = root.GetLastToken();
        var replacer = new SyntaxLastTokenReplacer(oldToken, newToken);
        var newRoot = (T)replacer.Visit(root);
        return newRoot;
    }

    private static int CountNonNullSlots(BelteSyntaxNode node) {
        return node.ChildNodesAndTokens().Count;
    }

    internal override BelteSyntaxNode Visit(BelteSyntaxNode node) {
        if (node is not null && !_found) {
            _count--;

            if (_count == 0) {
                if (node is SyntaxToken) {
                    _found = true;
                    return _newToken;
                }

                _count += CountNonNullSlots(node);
                return base.Visit(node);
            }
        }

        return node;
    }
}
