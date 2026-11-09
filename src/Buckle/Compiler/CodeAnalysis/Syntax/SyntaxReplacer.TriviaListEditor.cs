using System.Collections.Generic;

namespace Buckle.CodeAnalysis.Syntax;

internal static partial class SyntaxReplacer {
    private class TriviaListEditor : BaseListEditor {
        private readonly SyntaxTrivia _originalTrivia;
        private readonly IEnumerable<SyntaxTrivia> _newTrivia;

        internal TriviaListEditor(
            SyntaxTrivia originalTrivia,
            IEnumerable<SyntaxTrivia> newTrivia,
            ListEditKind editKind)
            : base(originalTrivia.span, editKind, true, originalTrivia.IsPartOfStructuredTrivia()) {
            _originalTrivia = originalTrivia;
            _newTrivia = newTrivia;
        }

        internal override SyntaxTriviaList VisitList(SyntaxTriviaList list) {
            var index = list.IndexOf(_originalTrivia);

            if (index >= 0 && index < list.Count) {
                switch (_editKind) {
                    case ListEditKind.Replace:
                        return list.ReplaceRange(_originalTrivia, _newTrivia);
                    case ListEditKind.InsertAfter:
                        return list.InsertRange(index + 1, _newTrivia);
                    case ListEditKind.InsertBefore:
                        return list.InsertRange(index, _newTrivia);
                }
            }

            return base.VisitList(list);
        }
    }
}
