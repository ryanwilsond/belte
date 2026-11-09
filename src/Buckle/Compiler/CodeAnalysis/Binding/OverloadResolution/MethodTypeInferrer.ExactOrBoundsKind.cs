
namespace Buckle.CodeAnalysis.Binding;

internal partial struct MethodTypeInferrer {
    private enum ExactOrBoundsKind {
        Exact,
        LowerBound,
        UpperBound,
    }
}
