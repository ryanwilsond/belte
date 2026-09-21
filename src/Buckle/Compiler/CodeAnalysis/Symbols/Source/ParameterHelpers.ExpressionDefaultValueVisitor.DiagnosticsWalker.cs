using Buckle.CodeAnalysis.Binding;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Symbols;

internal static partial class ParameterHelpers {
    internal sealed partial class ExpressionDefaultValueVisitor {
        private sealed class DiagnosticsWalker : BoundTreeWalkerWithStackGuard {
            private readonly ParameterSymbol _parameter;
            private readonly BelteDiagnosticQueue _diagnostics;

            internal bool reportedError;
            internal bool referencesPriorParameter;

            internal DiagnosticsWalker(ParameterSymbol parameter, BelteDiagnosticQueue diagnostics) {
                _parameter = parameter;
                _diagnostics = diagnostics;
            }

            internal override BoundNode VisitParameterExpression(BoundParameterExpression node) {
                var parameter = node.parameter;

                if (parameter.ordinal >= _parameter.ordinal &&
                    // TODO This is a hack because our binders are creating different LocalFunctionSymbols
                    // Which shouldn't be happening
                    parameter.containingSymbol.GetNonNullSyntaxNode() == _parameter.containingSymbol.GetNonNullSyntaxNode()) {
                    if (parameter.ordinal == _parameter.ordinal) {
                        _diagnostics.Push(Error.DefaultValueCannotReferenceParameter(
                            node.syntax.location,
                            _parameter.name
                        ));
                    } else {
                        _diagnostics.Push(Error.DefaultValueCannotReferenceLaterParameter(
                            node.syntax.location,
                            _parameter.name,
                            parameter.name
                        ));
                    }

                    reportedError = true;
                } else {
                    referencesPriorParameter = true;
                }

                return null;
            }
        }
    }
}
