using System.Collections.Immutable;
using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

internal readonly partial struct Blender {
    private struct Reader {
        private readonly Lexer _lexer;
        private Cursor _oldTreeCursor;
        private ImmutableStack<TextChangeRange> _changes;
        private int _newPosition;
        private int _changeDelta;
        private DirectiveStack _newDirectives;
        private DirectiveStack _oldDirectives;
        private LexerMode _newLexerDrivenMode;

        internal Reader(Blender blender) {
            _lexer = blender._lexer;
            _oldTreeCursor = blender._oldTreeCursor;
            _changes = blender._changes;
            _newPosition = blender._newPosition;
            _changeDelta = blender._changeDelta;
            _newDirectives = blender._newDirectives;
            _oldDirectives = blender._oldDirectives;
            _newLexerDrivenMode = blender._newLexerDrivenMode;
        }

        internal BlendedNode ReadNodeOrToken(LexerMode mode, bool asToken) {
            while (true) {
                if (_oldTreeCursor.isFinished)
                    return ReadNewToken(mode);

                if (_changeDelta < 0) {
                    SkipOldToken();
                } else if (_changeDelta > 0) {
                    return ReadNewToken(mode);
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
            _oldDirectives = node.ApplyDirectives(_oldDirectives);
            _oldTreeCursor = Cursor.MoveToNextSibling(_oldTreeCursor);

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

        private BlendedNode ReadNewToken(LexerMode mode) {
            var token = LexNewToken(mode);
            var width = token.fullWidth;
            _newPosition += width;
            _changeDelta -= width;

            SkipPastChanges();

            return CreateBlendedNode(null, token);
        }

        private SyntaxToken LexNewToken(LexerMode mode) {
            if (_lexer.position != _newPosition)
                _lexer.Reset(_newPosition, _newDirectives);

            var token = _lexer.LexNext(ref mode);
            _newDirectives = _lexer.directives;
            _newLexerDrivenMode = 0;
            return token;
        }

        private bool TryTakeOldNodeOrToken(bool asToken, out BlendedNode blendedNode) {
            if (asToken)
                _oldTreeCursor = _oldTreeCursor.MoveToFirstToken();

            var currentNodeOrToken = _oldTreeCursor.currentNodeOrToken;

            if (!CanReuse(currentNodeOrToken)) {
                blendedNode = default;
                return false;
            }

            _newPosition += currentNodeOrToken.fullWidth;
            _oldTreeCursor = Cursor.MoveToNextSibling(_oldTreeCursor);

            _newDirectives = currentNodeOrToken.ApplyDirectives(_newDirectives);
            _oldDirectives = currentNodeOrToken.ApplyDirectives(_oldDirectives);

            blendedNode = CreateBlendedNode(
                currentNodeOrToken.AsNode(),
                (SyntaxToken)currentNodeOrToken.AsToken()?.node
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
                    nodeOrToken.parent.containsDiagnostics)) {
                return false;
            }

            if (IsFabricatedToken(nodeOrToken.kind))
                return false;

            if ((nodeOrToken.isToken && nodeOrToken.AsToken().isFabricated) ||
                (nodeOrToken.isNode && IsIncomplete(nodeOrToken.AsNode()))) {
                return false;
            }

            if (!nodeOrToken.containsDirectives)
                return true;

            return _newDirectives.IncrementallyEquivalent(_oldDirectives);
        }

        private static bool IsIncomplete(SyntaxNode node) {
            return node.green.GetLastTerminal().isFabricated;
        }

        private bool IntersectsNextChange(SyntaxNodeOrToken node) {
            if (_changes.IsEmpty)
                return false;

            var oldSpan = node.fullSpan;
            var changeSpan = _changes.Peek().span;

            return oldSpan.IntersectsWith(changeSpan);
        }

        private static bool IsFabricatedToken(SyntaxKind kind) {
            switch (kind) {
                case SyntaxKind.GreaterThanGreaterThanToken:
                case SyntaxKind.GreaterThanGreaterThanGreaterThanToken:
                // TODO See parser todo comment about these 2 tokens
                // case SyntaxKind.GreaterThanGreaterThanEqualsToken:
                // case SyntaxKind.GreaterThanGreaterThanGreaterThanEqualsToken:
                case SyntaxKind.AsteriskAsteriskToken:
                case SyntaxKind.GreaterThanLessThanToken:
                case SyntaxKind.GreaterThanLessThanEqualsToken:
                case SyntaxKind.PeriodPeriodLessThanToken:
                case SyntaxKind.PeriodPeriodEqualsToken:
                    return true;
                default:
                    return SyntaxFacts.IsContextualKeyword(kind);
            }
        }

        private readonly BlendedNode CreateBlendedNode(SyntaxNode node, SyntaxToken token) {
            return new BlendedNode(
                node,
                token,
                new Blender(
                    _lexer,
                    _oldTreeCursor,
                    _changes,
                    _newPosition,
                    _changeDelta,
                    _newDirectives,
                    _oldDirectives,
                    _newLexerDrivenMode
                )
            );
        }
    }
}
