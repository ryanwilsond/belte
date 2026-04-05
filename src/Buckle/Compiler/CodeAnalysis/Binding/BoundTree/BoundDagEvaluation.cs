
namespace Buckle.CodeAnalysis.Binding;

internal partial class BoundDagEvaluation {
    internal virtual bool IsEquivalentTo(BoundDagEvaluation other) {
        return this == other || kind == other.kind;
    }
}
