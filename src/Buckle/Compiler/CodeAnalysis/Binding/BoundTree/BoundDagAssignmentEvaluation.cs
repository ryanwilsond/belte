using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Binding;

internal partial class BoundDagAssignmentEvaluation {
    public override int GetHashCode() {
        return Hash.Combine(base.GetHashCode(), target.GetHashCode());
    }

    internal override bool IsEquivalentTo(BoundDagEvaluation obj) {
        return base.IsEquivalentTo(obj) && target.Equals(((BoundDagAssignmentEvaluation)obj).target);
    }
}
