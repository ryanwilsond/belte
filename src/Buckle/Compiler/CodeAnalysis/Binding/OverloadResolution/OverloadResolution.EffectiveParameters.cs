using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

internal sealed partial class OverloadResolution {
    private readonly struct EffectiveParameters {
        internal readonly ImmutableArray<TypeWithAnnotations> parameterTypes;
        internal readonly ImmutableArray<RefKind> parameterRefKinds;
        internal readonly int firstParamsElementIndex;

        internal EffectiveParameters(
            ImmutableArray<TypeWithAnnotations> types,
            ImmutableArray<RefKind> refKinds,
            int firstParamsElementIndex) {
            parameterTypes = types;
            parameterRefKinds = refKinds;
            this.firstParamsElementIndex = firstParamsElementIndex;
        }
    }
}
