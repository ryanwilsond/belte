using System.Diagnostics;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Lowering;

internal sealed partial class CompileTimeLowerer {
    private sealed class CompileTimeExpressionWalker : BoundTreeWalkerWithStackGuard {
        internal bool invalidNode;
        internal Symbol firstInvalidSymbol;

        internal override BoundNode VisitDataContainerExpression(BoundDataContainerExpression node) {
            if (!invalidNode) {
                invalidNode = true;
                Debug.Assert(firstInvalidSymbol is null);
                firstInvalidSymbol = node.dataContainer;
            }

            return null;
        }

        internal override BoundNode VisitParameterExpression(BoundParameterExpression node) {
            if (!invalidNode) {
                invalidNode = true;
                Debug.Assert(firstInvalidSymbol is null);
                firstInvalidSymbol = node.parameter;
            }

            return null;
        }
    }
}
