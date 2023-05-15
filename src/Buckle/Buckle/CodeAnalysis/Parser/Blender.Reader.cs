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

                    if (_oldTreeCursor.currentNodeOrToken.isNode)
                        _oldTreeCursor = _oldTreeCursor.MoveToFirstChild();
                    else
                        SkipOldToken();
                }
            }
        }

        private void SkipOldToken() {
            _oldTreeCursor = _oldTreeCursor.MoveToFirstToken();
            var node = _oldTreeCursor.currentNodeOrToken;

            _changeDelta += node.fullWidth;
            _oldTreeCursor = _oldTreeCursor.MoveToNextSibling();

            SkipPastChanges();
        }

        private void SkipPastChanges() {
            var oldPosition = _oldTreeCursor.currentNodeOrToken.position;

            while (!_changes.IsEmpty && oldPosition >= _changes.Peek().span.end) {
                var change = _changes.Peek();
                _changes = _changes.Pop();
                _changeDelta += change.newLength - change.span.length;
            }
        }

        private BlendedNode ReadNewToken() {
            var token = LexNewToken();
            var width = token.fullWidth;
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

            var currentNodeOrToken = _oldTreeCursor.currentNodeOrToken;

            if (!CanReuse(currentNodeOrToken)) {
                blendedNode = null;
                return false;
            }

            _newPosition += currentNodeOrToken.fullWidth;
            _oldTreeCursor = _oldTreeCursor.MoveToNextSibling();

            blendedNode = CreateBlendedNode(
                currentNodeOrToken.AsNode(),
                (InternalSyntax.SyntaxToken)currentNodeOrToken.AsToken().node
            );

            return true;
        }

        private bool CanReuse(SyntaxNodeOrToken nodeOrToken) {
            // Doing the least performant checks last
            if (nodeOrToken.fullWidth == 0)
                return false;

            if (nodeOrToken.kind == SyntaxKind.None || nodeOrToken.kind == SyntaxKind.BadToken)
                return false;

            if (IntersectsNextChange(nodeOrToken))
                return false;

            if (nodeOrToken.containsDiagnostics ||
                (nodeOrToken.isToken &&
                    nodeOrToken.AsToken().node.containsSkippedText &&
                    nodeOrToken.parent.containsDiagnostics))
                return false;

            if ((nodeOrToken.isToken && nodeOrToken.AsToken().isFabricated) ||
                (nodeOrToken.isNode && IsIncomplete(nodeOrToken.AsNode())))
                return false;

            return true;
        }

        private static bool IsIncomplete(SyntaxNode node) {
            return node.green.GetLastTerminal().isFabricated;
        }

        private bool IntersectsNextChange(SyntaxNodeOrToken node) {
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
