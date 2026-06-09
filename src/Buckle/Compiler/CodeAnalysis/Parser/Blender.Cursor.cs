using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

internal readonly partial struct Blender {
    private readonly struct Cursor {
        internal readonly SyntaxNodeOrToken currentNodeOrToken;
        private readonly int _indexInParent;

        private Cursor(SyntaxNodeOrToken node, int indexIntParent) {
            currentNodeOrToken = node;
            _indexInParent = indexIntParent;
        }

        internal static Cursor FromRoot(SyntaxNode node) {
            return new Cursor(node, 0);
        }

        internal bool isFinished
            => currentNodeOrToken.kind == SyntaxKind.None ||
               currentNodeOrToken.kind == SyntaxKind.EndOfFileToken;

        private Cursor TryFindNextNonZeroWidthOrIsEndOfFileSibling() {
            if (currentNodeOrToken.parent is not null) {
                var siblings = currentNodeOrToken.parent.ChildNodesAndTokens();

                for (int i = _indexInParent + 1, n = siblings.Count; i < n; i++) {
                    var sibling = siblings[i];

                    if (IsNonZeroWidthOrIsEndOfFile(sibling))
                        return new Cursor(sibling, i);
                }
            }

            return default;
        }

        internal static Cursor MoveToNextSibling(Cursor cursor) {
            while (cursor.currentNodeOrToken.underlyingNode is not null) {
                var nextSibling = cursor.TryFindNextNonZeroWidthOrIsEndOfFileSibling();

                if (nextSibling.currentNodeOrToken.underlyingNode is not null)
                    return nextSibling;

                cursor = cursor.MoveToParent();
            }

            return default;
        }

        internal Cursor MoveToFirstChild() {
            var node = currentNodeOrToken.AsNode();

            if (node.kind == SyntaxKind.InterpolatedStringExpression) {
                var greenToken = Lexer.DereadInterpolatedString((InterpolatedStringExpressionSyntax)node.green);
                var redToken = new Syntax.SyntaxToken(node.parent, greenToken, node.position, _indexInParent);
                return new Cursor(redToken, _indexInParent);
            }

            if (node.slotCount > 0) {
                var child = Syntax.ChildSyntaxList.ItemInternal(node, 0);

                if (IsNonZeroWidthOrIsEndOfFile(child))
                    return new Cursor(child, 0);
            }

            var index = 0;
            foreach (var child in currentNodeOrToken.ChildNodesAndTokens()) {
                if (IsNonZeroWidthOrIsEndOfFile(child))
                    return new Cursor(child, index);

                index++;
            }

            return new Cursor();
        }

        internal Cursor MoveToFirstToken() {
            var cursor = this;

            if (!cursor.isFinished) {
                for (var node = cursor.currentNodeOrToken;
                    node.kind != SyntaxKind.None && !SyntaxFacts.IsToken(node.kind);
                    node = cursor.currentNodeOrToken) {
                    cursor = cursor.MoveToFirstChild();
                }
            }

            return cursor;
        }

        private Cursor MoveToParent() {
            var parent = currentNodeOrToken.parent;
            var index = IndexOfNodeInParent(parent);
            return new Cursor(parent, index);
        }

        private static int IndexOfNodeInParent(SyntaxNode node) {
            if (node.parent is null)
                return 0;

            var children = node.parent.ChildNodesAndTokens();
            var index = SyntaxNodeOrToken.GetFirstChildIndexSpanningPosition(children, node.position);

            for (int i = index, n = children.Count; i < n; i++) {
                var child = children[i];

                if (child == node)
                    return i;
            }

            throw ExceptionUtilities.Unreachable();
        }

        private static bool IsNonZeroWidthOrIsEndOfFile(SyntaxNodeOrToken token) {
            return token.kind == SyntaxKind.EndOfFileToken || token.fullWidth != 0;
        }
    }
}
