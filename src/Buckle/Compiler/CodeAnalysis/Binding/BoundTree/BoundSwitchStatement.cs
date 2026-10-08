using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Binding;

internal partial class BoundSwitchStatement {
    internal BoundDecisionDag GetDecisionDagForLowering(Compilation compilation) {
        var decisionDag = reachabilityDecisionDag;

        if (decisionDag.ContainsAnySynthesizedNodes()) {
            decisionDag = DecisionDagBuilder.CreateDecisionDagForSwitchStatement(
                compilation,
                syntax,
                expression,
                switchSections,
                defaultLabel?.label ?? breakLabel,
                BelteDiagnosticQueue.Discarded,
                forLowering: true
            );
        }

        return decisionDag;
    }
}
