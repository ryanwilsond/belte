using System.Collections.Immutable;
using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

internal sealed partial class Blender {
    private sealed class Reader {
        private readonly Lexer _lexer;
        private Cursor _oldTreeCursor;
        private ImmutableStack<TextChangeRange> _changes;
        private int _newPosition;
        private int _changeDelta;

        internal Reader(Blender blender) {
            _lexer = blender._lexer;
            _oldTreeCursor = blender._oldTreeCursor;
            _changes = blender._changes;
            _newPosition = blender._newPosition;
            _changeDelta = blender._changeDelta;
        }

        internal BlendedNode ReadNodeOrToken(bool asToken) {
            while (true) {
                if (_oldTreeCursor.isFinished)
                    return ReadNewToken();

                if (_changeDelta < 0) {
                    SkipOldToken();
                } else if (_changeDelta > 0) {
                    return ReadNewToken();
                } else {

                    if (TryTakeOldNodeOrToken(asToken, out var blendedNode))
                        return blendedNode;

                    if (!_oldTreeCursor.currentNode.kind.IsToken())
                        _oldTreeCursor = _oldTreeCursor.MoveToFirstChild();
                    else
                        SkipOldToken();
                }
            }
        }

        private void SkipOldToken() {
            _oldTreeCursor = _oldTreeCursor.MoveToFirstToken();
            var node = _oldTreeCursor.currentNode;

            _changeDelta += node.fullSpan.length;
            _oldTreeCursor = _oldTreeCursor.MoveToNextSibling();

            SkipPastChanges();
        }

        private void SkipPastChanges() {
            var oldPosition = _oldTreeCursor.currentNode.fullSpan.start;

            while (!_changes.IsEmpty && oldPosition >= _changes.Peek().span.end) {
                var change = _changes.Peek();
                _changes = _changes.Pop();
                _changeDelta += change.newLength - change.span.length;
            }
        }

        private BlendedNode ReadNewToken() {
            var token = LexNewToken();
            var width = token.fullSpan.length;
            _newPosition += width;
            _changeDelta -= width;

            SkipPastChanges();

            return CreateBlendedNode(null, token);
        }

        private SyntaxToken LexNewToken() {
            if (_lexer.position != _newPosition)
                _lexer.Move(_newPosition);

            var token = _lexer.LexNext();
            return token;
        }

        private bool TryTakeOldNodeOrToken(bool asToken, out BlendedNode blendedNode) {
            if (asToken)
                _oldTreeCursor = _oldTreeCursor.MoveToFirstToken();

            var node = _oldTreeCursor.currentNode;

            if (!CanReuse(node)) {
                blendedNode = null;
                return false;
            }

            _newPosition += node.fullSpan.length;
            _oldTreeCursor = _oldTreeCursor.MoveToNextSibling();

            blendedNode = CreateBlendedNode(node.kind.IsToken() ? null : node, node as SyntaxToken);
            return true;
        }

        private bool CanReuse(SyntaxNode node) {
            if (node.fullSpan.length == 0)
                return false;

            if (IntersectsNextChange(node))
                return false;

            if (node.kind.IsToken() && (node as SyntaxToken).isFabricated)
                return false;

            if (node.GetLastToken().isFabricated)
                return false;

            return true;
        }

        private bool IntersectsNextChange(SyntaxNode node) {
            if (_changes.IsEmpty)
                return false;

            var oldSpan = node.fullSpan;
            var changeSpan = _changes.Peek().span;

            return oldSpan.OverlapsWith(changeSpan);
        }

        private BlendedNode CreateBlendedNode(SyntaxNode node, SyntaxToken token) {
            return new BlendedNode(
                node, token, new Blender(_lexer, _oldTreeCursor, _changes, _newPosition, _changeDelta)
            );
        }
    }
}
