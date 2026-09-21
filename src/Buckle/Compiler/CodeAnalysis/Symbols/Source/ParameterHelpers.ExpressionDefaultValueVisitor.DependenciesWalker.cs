using Buckle.CodeAnalysis.Binding;

namespace Buckle.CodeAnalysis.Symbols;

internal static partial class ParameterHelpers {
    internal sealed partial class ExpressionDefaultValueVisitor {
        private sealed class DependenciesWalker : BoundTreeWalkerWithStackGuard {
            private readonly ParameterSymbol _parameter;
            private readonly bool[] _dependencies;

            internal DependenciesWalker(ParameterSymbol parameter, bool[] dependencies) {
                _parameter = parameter;
                _dependencies = dependencies;
            }

            internal override BoundNode VisitParameterExpression(BoundParameterExpression node) {
                var parameter = node.parameter;

                if (parameter.containingSymbol.GetNonNullSyntaxNode()
                        == _parameter.containingSymbol.GetNonNullSyntaxNode()) {
                    _dependencies[parameter.ordinal] = true;
                }

                return null;
            }
        }
    }
}
