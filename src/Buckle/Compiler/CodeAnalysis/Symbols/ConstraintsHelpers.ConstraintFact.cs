using System.Collections.Generic;
using Buckle.CodeAnalysis.Binding;

namespace Buckle.CodeAnalysis.Symbols;

internal static partial class ConstraintsHelpers {
    internal sealed class ConstraintFact {
        internal Symbol pertaining;
        internal List<ConstraintRange> ranges;
        internal List<BoundExpression> coveredConstraints;

        internal ConstraintFact(
            Symbol pertaining,
            List<ConstraintRange> ranges,
            List<BoundExpression> coveredConstraints) {
            this.pertaining = pertaining;
            this.ranges = ranges;
            this.coveredConstraints = coveredConstraints;
        }
    }
}
