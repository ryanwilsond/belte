using Buckle.CodeAnalysis.Binding;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Symbols;

internal static partial class ParameterHelpers {
    internal sealed partial class ExpressionDefaultValueVisitor {
        private sealed class ReplacementRewriter : BoundTreeRewriterWithStackGuard {
            private readonly ParameterSymbol _parameter;
            private readonly ArrayBuilder<BoundExpression> _arguments;

            internal ReplacementRewriter(ParameterSymbol parameter, ArrayBuilder<BoundExpression> arguments) {
                _parameter = parameter;
                _arguments = arguments;
            }

            internal override BoundNode VisitParameterExpression(BoundParameterExpression node) {
                var parameter = node.parameter;

                if (parameter.containingSymbol.GetNonNullSyntaxNode()
                        == _parameter.containingSymbol.GetNonNullSyntaxNode()) {
                    return Visit(_arguments[parameter.ordinal]);
                }

                return node;
            }
        }
    }
}
