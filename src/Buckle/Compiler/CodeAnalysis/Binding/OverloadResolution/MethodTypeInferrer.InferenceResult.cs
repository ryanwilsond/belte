
namespace Buckle.CodeAnalysis.Binding;

internal partial struct MethodTypeInferrer {
    private enum InferenceResult {
        InferenceFailed,
        MadeProgress,
        NoProgress,
        Success
    }
}
