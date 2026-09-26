using System.Collections.Immutable;
using Buckle.CodeAnalysis.Binding;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Symbols;

internal static partial class ConstraintsHelpers {
    private sealed class EvaluationArgs {
        internal TemplateMap substitution;
        internal ImmutableArray<BoundExpressionOrTypeOrConstant> arguments;
        internal BelteDiagnosticQueue diagnostics;
        internal bool failedToSubstituteTemplateParameter;

        internal EvaluationArgs(
            TemplateMap substitution,
            ImmutableArray<BoundExpressionOrTypeOrConstant> arguments,
            BelteDiagnosticQueue diagnostics) {
            this.substitution = substitution;
            this.arguments = arguments;
            this.diagnostics = diagnostics;
        }
    }
}
