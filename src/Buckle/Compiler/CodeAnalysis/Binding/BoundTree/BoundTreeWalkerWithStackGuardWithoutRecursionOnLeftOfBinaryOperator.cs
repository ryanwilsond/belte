using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Binding;

internal abstract class BoundTreeWalkerWithStackGuardWithoutRecursionOnLeftOfBinaryOperator
    : BoundTreeWalkerWithStackGuard {
    private protected BoundTreeWalkerWithStackGuardWithoutRecursionOnLeftOfBinaryOperator() { }

    private protected BoundTreeWalkerWithStackGuardWithoutRecursionOnLeftOfBinaryOperator(int recursionDepth)
        : base(recursionDepth) { }

    internal sealed override BoundNode VisitBinaryOperator(BoundBinaryOperator node) {
        if (node.left.kind != BoundKind.BinaryOperator)
            return base.VisitBinaryOperator(node);

        var rightOperands = ArrayBuilder<BoundExpression>.GetInstance();

        rightOperands.Push(node.right);

        var binary = (BoundBinaryOperator)node.left;

        BeforeVisitingSkippedBoundBinaryOperatorChildren(binary);
        rightOperands.Push(binary.right);

        var current = binary.left;

        while (current.kind == BoundKind.BinaryOperator) {
            binary = (BoundBinaryOperator)current;
            BeforeVisitingSkippedBoundBinaryOperatorChildren(binary);
            rightOperands.Push(binary.right);
            current = binary.left;
        }

        Visit(current);

        while (rightOperands.Count > 0)
            Visit(rightOperands.Pop());

        rightOperands.Free();
        return null;
    }

    private protected virtual void BeforeVisitingSkippedBoundBinaryOperatorChildren(BoundBinaryOperator node) { }

    internal sealed override BoundNode VisitCallExpression(BoundCallExpression node) {
        if (node.receiver is BoundCallExpression receiver1) {
            var calls = ArrayBuilder<BoundCallExpression>.GetInstance();

            calls.Push(node);

            node = receiver1;
            while (node.receiver is BoundCallExpression receiver2) {
                BeforeVisitingSkippedBoundCallChildren(node);
                calls.Push(node);
                node = receiver2;
            }

            BeforeVisitingSkippedBoundCallChildren(node);

            VisitReceiver(node);

            do {
                VisitArguments(node);
            } while (calls.TryPop(out node!));

            calls.Free();
        } else {
            VisitReceiver(node);
            VisitArguments(node);
        }

        return null;
    }

    private protected virtual void BeforeVisitingSkippedBoundCallChildren(BoundCallExpression node) { }

    private protected virtual void VisitReceiver(BoundCallExpression node) {
        Visit(node.receiver);
    }

    private protected virtual void VisitArguments(BoundCallExpression node) {
        VisitList(node.arguments);
    }
}
