using System;
using System.Diagnostics;
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
            // TODO Does this happen often enough to warrant this
            // throw new CancelledByStackGuardException(ex, node);
            throw ex;
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
}
