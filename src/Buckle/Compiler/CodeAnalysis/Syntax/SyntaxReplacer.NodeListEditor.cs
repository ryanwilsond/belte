using System.Collections.Generic;
using System.Linq;

namespace Buckle.CodeAnalysis.Syntax;

internal static partial class SyntaxReplacer {
    private class NodeListEditor : BaseListEditor {
        private readonly SyntaxNode _originalNode;
        private readonly IEnumerable<SyntaxNode> _newNodes;

        internal NodeListEditor(
            SyntaxNode originalNode,
            IEnumerable<SyntaxNode> replacementNodes,
            ListEditKind editKind)
            : base(originalNode.span, editKind, false, originalNode.IsPartOfStructuredTrivia()) {
            _originalNode = originalNode;
            _newNodes = replacementNodes;
        }

        internal override SyntaxNode Visit(SyntaxNode node) {
            if (node == _originalNode)
                throw GetItemNotListElementException();

            return base.Visit(node);
        }

        internal override SeparatedSyntaxList<TNode> VisitList<TNode>(SeparatedSyntaxList<TNode> list) {
            if (_originalNode is TNode node) {
                var index = list.IndexOf(node);

                if (index >= 0 && index < list.Count) {
                    switch (_editKind) {
                        case ListEditKind.Replace:
                            return list.ReplaceRange(node, _newNodes.Cast<TNode>());
                        case ListEditKind.InsertAfter:
                            return list.InsertRange(index + 1, _newNodes.Cast<TNode>());
                        case ListEditKind.InsertBefore:
                            return list.InsertRange(index, _newNodes.Cast<TNode>());
                    }
                }
            }

            return base.VisitList(list);
        }

        internal override SyntaxList<TNode> VisitList<TNode>(SyntaxList<TNode> list) {
            if (_originalNode is TNode node) {
                var index = list.IndexOf(node);

                if (index >= 0 && index < list.Count) {
                    switch (_editKind) {
                        case ListEditKind.Replace:
                            return list.ReplaceRange(node, _newNodes.Cast<TNode>());
                        case ListEditKind.InsertAfter:
                            return list.InsertRange(index + 1, _newNodes.Cast<TNode>());
                        case ListEditKind.InsertBefore:
                            return list.InsertRange(index, _newNodes.Cast<TNode>());
                    }
                }
            }

            return base.VisitList(list);
        }
    }
}
