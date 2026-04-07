using Buckle.CodeAnalysis.Symbols;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Binding;

internal partial class BoundDagEvaluation {
    internal Symbol symbol {
        get {
            var result = this switch {
                BoundDagTypeEvaluation e => e.type,
                BoundDagAssignmentEvaluation => null,
                _ => throw ExceptionUtilities.UnexpectedValue(kind)
            };

            return result;
        }
    }

    public sealed override bool Equals(object obj) {
        return obj is BoundDagEvaluation other && Equals(other);
    }

    internal bool Equals(BoundDagEvaluation other) {
        return this == other ||
            IsEquivalentTo(other) &&
            input.Equals(other.input);
    }

    internal virtual bool IsEquivalentTo(BoundDagEvaluation other) {
        return this == other || kind == other.kind;
    }

    public override int GetHashCode() {
        return Hash.Combine(input.GetHashCode(), symbol?.GetHashCode() ?? 0);
    }
}
