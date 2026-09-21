using Buckle.CodeAnalysis.Binding;
using Buckle.Diagnostics;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Symbols;

internal static partial class ParameterHelpers {
    internal sealed partial class ExpressionDefaultValueVisitor {
        internal static bool ReportDiagnostics(
            ParameterSymbol parameter,
            BoundExpression expression,
            BelteDiagnosticQueue diagnostics) {
            var walker = new DiagnosticsWalker(parameter, diagnostics);
            walker.Visit(expression);

            if (!walker.reportedError && !walker.referencesPriorParameter)
                diagnostics.Push(Error.DefaultValueMustReferenceParameter(expression.syntax.location, parameter.name));

            return walker.reportedError;
        }

        internal static void FindDependencies(
            ParameterSymbol parameter,
            BoundExpression expression,
            bool[] dependencies) {
            var walker = new DependenciesWalker(parameter, dependencies);
            walker.Visit(expression);
        }

        internal static BoundExpression ReplaceArguments(
            ParameterSymbol parameter,
            BoundExpression argument,
            ArrayBuilder<BoundExpression> arguments) {
            var walker = new ReplacementRewriter(parameter, arguments);
            return (BoundExpression)walker.Visit(argument);
        }
    }
}
