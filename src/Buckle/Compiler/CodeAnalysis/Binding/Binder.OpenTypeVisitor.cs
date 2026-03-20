using System.Collections.Generic;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Binding;

internal partial class Binder {
    private protected sealed class OpenTypeVisitor : SyntaxVisitor {
        private Dictionary<TemplateNameSyntax, bool> _allowedMap;
        private bool _seenConstructed;

        internal static void Visit(ExpressionSyntax typeSyntax, out Dictionary<TemplateNameSyntax, bool> allowedMap) {
            var visitor = new OpenTypeVisitor();
            visitor.Visit(typeSyntax);
            allowedMap = visitor._allowedMap;
        }

        internal override void VisitTemplateName(TemplateNameSyntax node) {
            var templateArguments = node.templateArgumentList.arguments;

            // isUnboundTemplateName would check for omitted arguments which require more binding
            // however, because we allow any expression, all arguments require this same level of binding
            // so we treat this as always true
            _allowedMap ??= [];
            _allowedMap[node] = !_seenConstructed;
            // if (node.isUnboundTemplateName) {
            //     _allowedMap ??= [];
            //     _allowedMap[node] = !_seenConstructed;
            // } else {
            //     _seenConstructed = true;

            //     foreach (var arg in templateArguments)
            //         Visit(arg);
            // }
        }

        internal override void VisitQualifiedName(QualifiedNameSyntax node) {
            var seenConstructedBeforeRight = _seenConstructed;

            // Visit Right first because it's smaller (to make backtracking cheaper).
            Visit(node.right);

            var seenConstructedBeforeLeft = _seenConstructed;

            Visit(node.left);

            // If the first time we saw a constructed type was in Left, then we need to re-visit Right
            if (!seenConstructedBeforeRight && !seenConstructedBeforeLeft && _seenConstructed)
                Visit(node.right);
        }

        internal override void VisitArrayType(ArrayTypeSyntax node) {
            _seenConstructed = true;
            Visit(node.elementType);
        }

        internal override void VisitNonNullableType(NonNullableTypeSyntax node) {
            Visit(node.type);
        }
    }
}
