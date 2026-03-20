using System;
using System.Diagnostics;
using System.Linq;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Binding;

internal abstract partial class BoundTreeVisitor {
    private protected BoundTreeVisitor() { }

    [DebuggerHidden]
    internal virtual BoundNode Visit(BoundNode node) {
        if (node is not null)
            return node.Accept(this);

        return null;
    }

    [DebuggerHidden]
    internal virtual BoundNode DefaultVisit(BoundNode node) {
        return null;
    }

    [DebuggerStepThrough]
    private protected BoundExpression VisitExpressionWithStackGuard(ref int recursionDepth, BoundExpression node) {
        BoundExpression result;
        recursionDepth++;

        if (recursionDepth > 1 || !ConvertInsufficientExecutionStackExceptionToCancelledByStackGuardException()) {
            EnsureSufficientExecutionStack(recursionDepth);
            result = VisitExpressionWithoutStackGuard(node);
        } else {
            result = VisitExpressionWithStackGuard(node);
        }

        recursionDepth--;
        return result;
    }

    [DebuggerStepThrough]
    private BoundExpression VisitExpressionWithStackGuard(BoundExpression node) {
        try {
            return VisitExpressionWithoutStackGuard(node);
        } catch (InsufficientExecutionStackException ex) {
            throw new CancelledByStackGuardException(ex, node);
        }
    }

    private protected virtual void EnsureSufficientExecutionStack(int recursionDepth) {
        StackGuard.EnsureSufficientExecutionStack(recursionDepth);
    }

    private protected virtual bool ConvertInsufficientExecutionStackExceptionToCancelledByStackGuardException() {
        return true;
    }

    private protected virtual BoundExpression VisitExpressionWithoutStackGuard(BoundExpression node) {
        return (BoundExpression)Visit(node);
    }

    internal class CancelledByStackGuardException : Exception {
        internal readonly BoundNode node;

        internal CancelledByStackGuardException(Exception inner, BoundNode node) : base(inner.Message, inner) {
            this.node = node;
        }

        internal void AddAnError(BelteDiagnosticQueue _) {
            // TODO Might want to add some higher level code that calls AddAnError on all thrown exceptions
            // diagnostics.Add(ErrorCode.ERR_InsufficientStack, GetTooLongOrComplexExpressionErrorLocation(node));
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
