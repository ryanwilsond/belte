using Buckle.CodeAnalysis.Binding;

namespace Buckle.CodeAnalysis.Evaluating;

internal sealed partial class Evaluator {
    private sealed class ExpressionInvariantVisitor : BoundTreeVisitor<object, bool> {
        internal static readonly ExpressionInvariantVisitor Instance = new();

        internal override bool VisitPointerIndirectionOperator(BoundPointerIndirectionOperator node, object arg) {
            return true;
        }

        internal override bool VisitPointerIndexAccessExpression(BoundPointerIndexAccessExpression node, object arg) {
            return true;
        }

        internal override bool VisitCastExpression(BoundCastExpression node, object arg) {
            return Visit(node.operand, arg);
        }

        internal override bool VisitArrayAccessExpression(BoundArrayAccessExpression node, object arg) {
            return Visit(node.receiver, arg) || Visit(node.index, arg);
        }

        internal override bool VisitUnaryOperator(BoundUnaryOperator node, object arg) {
            return Visit(node.operand, arg);
        }

        internal override bool VisitBinaryOperator(BoundBinaryOperator node, object arg) {
            return Visit(node.left, arg) || Visit(node.right, arg);
        }

        internal override bool VisitAssignmentOperator(BoundAssignmentOperator node, object arg) {
            return Visit(node.left, arg) || Visit(node.right, arg);
        }
    }
}
