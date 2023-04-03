using System.Linq;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

internal sealed partial class Blender {
    private sealed class Cursor {
        internal readonly SyntaxNode currentNode;

        private readonly int _indexInParent;

        internal Cursor() { }

        private Cursor(SyntaxNode node, int indexIntParent) {
            this.currentNode = node;
            _indexInParent = indexIntParent;
        }

        internal static Cursor FromRoot(SyntaxNode node) {
            return new Cursor(node, 0);
        }

        internal bool isFinished => currentNode.kind == SyntaxKind.EndOfFileToken;

        internal Cursor MoveToNextSibling() {
            if (currentNode.parent != null) {
                var siblings = currentNode.parent.GetChildren().ToArray();

                for (int i = _indexInParent + 1, n = siblings.Length; i < n; i++) {
                    var sibling = siblings[i];

                    if (IsNonZeroWidthOrIsEndOfFile(sibling))
                        return new Cursor(sibling, i);
                }

                return MoveToParent().MoveToNextSibling();
            }

            return new Cursor();
        }

        internal Cursor MoveToFirstChild() {
            var children = currentNode.GetChildren().ToArray();

            if (children.Any()) {
                var child = children[0];

                if (IsNonZeroWidthOrIsEndOfFile(child))
                    return new Cursor(child, 0);
            }

            int index = 0;
            foreach (var child in children) {
                if (IsNonZeroWidthOrIsEndOfFile(child))
                    return new Cursor(child, index);

                index++;
            }

            return new Cursor();
        }

        internal Cursor MoveToFirstToken() {
            var cursor = this;

            if (!cursor.isFinished) {
                for (var node = cursor.currentNode; !node.kind.IsToken(); node = cursor.currentNode)
                    cursor = cursor.MoveToFirstChild();
            }

            return cursor;
        }

        private Cursor MoveToParent() {
            var parent = currentNode.parent;
            var index = IndexOfNodeInParent(parent);
            return new Cursor(parent, index);
        }

        private static int IndexOfNodeInParent(SyntaxNode node) {
            if (node.parent == null)
                return 0;

            var children = node.parent.GetChildren().ToArray();
            var index = SyntaxNode.GetFirstChildIndexSpanningPosition(children, node.fullSpan.start);

            for (int i = index, n = children.Length; i < n; i++) {
                var child = children[i];

                if (child == node)
                    return i;
            }

            throw ExceptionUtilities.Unreachable();
        }

        private static bool IsNonZeroWidthOrIsEndOfFile(SyntaxNode token) {
            return token.kind == SyntaxKind.EndOfFileToken || token.fullSpan.length != 0;
        }
    }
}
