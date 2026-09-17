using System;
using System.Linq;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Binding;

internal abstract partial class BoundTreeVisitor {
    internal class CancelledByStackGuardException : Exception {
        internal readonly BoundNode node;

        internal CancelledByStackGuardException(Exception inner, BoundNode node) : base(inner.Message, inner) {
            this.node = node;
        }

        internal void AddAnError(BelteDiagnosticQueue diagnostics) {
            diagnostics.Push(Error.InsufficientStack(GetTooLongOrComplexExpressionErrorLocation(node)));
        }

        internal static TextLocation GetTooLongOrComplexExpressionErrorLocation(BoundNode node) {
            var syntax = node.syntax;

            if (syntax is not ExpressionSyntax) {
                syntax = syntax.DescendantNodes(n => n is not ExpressionSyntax)
                    .OfType<ExpressionSyntax>().FirstOrDefault() ?? syntax;
            }

            return syntax.GetFirstToken().location;
        }
    }
}
