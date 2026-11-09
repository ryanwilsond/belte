using System.Collections.Generic;

namespace Buckle.CodeAnalysis.Syntax;

internal static partial class SyntaxReplacer {
    private class TokenListEditor : BaseListEditor {
        private readonly SyntaxToken _originalToken;
        private readonly IEnumerable<SyntaxToken> _newTokens;

        internal TokenListEditor(
            SyntaxToken originalToken,
            IEnumerable<SyntaxToken> newTokens,
            ListEditKind editKind)
            : base(originalToken.span, editKind, false, originalToken.IsPartOfStructuredTrivia()) {
            _originalToken = originalToken;
            _newTokens = newTokens;
        }

        internal override SyntaxToken VisitToken(SyntaxToken token) {
            if (token == _originalToken) {
                throw GetTokenNotListElementException();
            }

            return base.VisitToken(token);
        }

        internal override SyntaxTokenList VisitList(SyntaxTokenList list) {
            var index = list.IndexOf(_originalToken);

            if (index >= 0 && index < list.Count) {
                switch (_editKind) {
                    case ListEditKind.Replace:
                        return list.ReplaceRange(_originalToken, _newTokens);
                    case ListEditKind.InsertAfter:
                        return list.InsertRange(index + 1, _newTokens);
                    case ListEditKind.InsertBefore:
                        return list.InsertRange(index, _newTokens);
                }
            }

            return base.VisitList(list);
        }
    }
}
