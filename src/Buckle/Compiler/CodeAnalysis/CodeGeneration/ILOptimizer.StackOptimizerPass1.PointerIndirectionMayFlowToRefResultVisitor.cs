using Buckle.CodeAnalysis.Binding;

namespace Buckle.CodeAnalysis.CodeGeneration;

internal sealed partial class ILOptimizer {
    internal sealed partial class StackOptimizerPass1 {
        private sealed class PointerIndirectionMayFlowToRefResultVisitor : BoundTreeWalkerWithStackGuardWithoutRecursionOnLeftOfBinaryOperator {
            private bool _pointerIndirectionMayFlowToRefResult;

            private PointerIndirectionMayFlowToRefResultVisitor(int recursionDepth) : base(recursionDepth) { }

            internal static bool Check(BoundExpression expression, int recursionDepth) {
                var visitor = new PointerIndirectionMayFlowToRefResultVisitor(recursionDepth);
                visitor.Visit(expression);
                return visitor._pointerIndirectionMayFlowToRefResult;
            }

            internal override BoundNode Visit(BoundNode node) {
                if (_pointerIndirectionMayFlowToRefResult)
                    return node;

                return base.Visit(node);
            }

            internal override BoundNode VisitPointerIndirectionOperator(BoundPointerIndirectionOperator node) {
                _pointerIndirectionMayFlowToRefResult = true;
                return node;
            }

            internal override BoundNode VisitCallExpression(BoundCallExpression node) {
                return node;
            }

            internal override BoundNode VisitFunctionPointerCallExpression(BoundFunctionPointerCallExpression node) {
                return node;
            }
        }
    }
}
