using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Binding;

internal partial class BoundSwitchStatement {
    internal BoundDecisionDag GetDecisionDagForLowering() {
        var decisionDag = reachabilityDecisionDag;

        if (decisionDag.ContainsAnySynthesizedNodes()) {
            decisionDag = DecisionDagBuilder.CreateDecisionDagForSwitchStatement(
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
