using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.CodeGeneration;

internal sealed partial class ILOptimizer {
    internal sealed partial class StackOptimizerPass1 {
        private sealed class LocalUsedWalker : BoundTreeWalkerWithStackGuardWithoutRecursionOnLeftOfBinaryOperator {
            private readonly DataContainerSymbol _local;
            private bool _found;

            internal LocalUsedWalker(DataContainerSymbol local, int recursionDepth)
                : base(recursionDepth) {
                _local = local;
            }

            internal bool IsLocalUsedIn(BoundNode node) {
                _found = false;
                Visit(node);
                return _found;
            }

            internal override BoundNode Visit(BoundNode node) {
                if (!_found) {
                    return base.Visit(node);
                }

                return null;
            }

            internal override BoundNode VisitDataContainerExpression(BoundDataContainerExpression node) {
                if (node.dataContainer == _local)
                    _found = true;

                return null;
            }
        }
    }
}
