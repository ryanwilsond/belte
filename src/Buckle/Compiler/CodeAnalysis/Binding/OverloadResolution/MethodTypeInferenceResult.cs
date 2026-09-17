using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

internal readonly struct MethodTypeInferenceResult {
    internal readonly ImmutableArray<TypeOrConstant> inferredTypeArguments;
    internal readonly bool hasTypeArgumentInferredFromFunctionType;
    internal readonly bool success;

    internal MethodTypeInferenceResult(
        bool success,
        ImmutableArray<TypeOrConstant> inferredTypeArguments,
        bool hasTypeArgumentInferredFromFunctionType) {
        this.success = success;
        this.inferredTypeArguments = inferredTypeArguments;
        this.hasTypeArgumentInferredFromFunctionType = hasTypeArgumentInferredFromFunctionType;
    }
}
