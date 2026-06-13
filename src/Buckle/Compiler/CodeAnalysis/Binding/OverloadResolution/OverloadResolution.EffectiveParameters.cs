using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

internal sealed partial class OverloadResolution {
    internal readonly struct EffectiveParameters {
        internal readonly ImmutableArray<TypeWithAnnotations> parameterTypes;
        internal readonly ImmutableArray<RefKind> parameterRefKinds;
        internal readonly ImmutableArray<bool> parameterConstness;
        internal readonly int firstParamsElementIndex;

        internal EffectiveParameters(
            ImmutableArray<TypeWithAnnotations> types,
            ImmutableArray<RefKind> refKinds,
            ImmutableArray<bool> constness,
            int firstParamsElementIndex) {
            parameterTypes = types;
            parameterRefKinds = refKinds;
            parameterConstness = constness;
            this.firstParamsElementIndex = firstParamsElementIndex;
        }
    }
}
