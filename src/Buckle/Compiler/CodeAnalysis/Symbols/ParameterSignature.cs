using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Symbols;

internal class ParameterSignature {
    internal static readonly ParameterSignature NoParams = new ParameterSignature([], default);

    internal readonly ImmutableArray<TypeWithAnnotations> parameterTypesWithAnnotations;
    internal readonly ImmutableArray<RefKind> parameterRefKinds;

    private ParameterSignature(
        ImmutableArray<TypeWithAnnotations> parameterTypesWithAnnotations,
        ImmutableArray<RefKind> parameterRefKinds) {
        this.parameterTypesWithAnnotations = parameterTypesWithAnnotations;
        this.parameterRefKinds = parameterRefKinds;
    }

    private static ParameterSignature MakeParamTypesAndRefKinds(ImmutableArray<ParameterSymbol> parameters) {
        if (parameters.Length == 0)
            return NoParams;

        var types = ArrayBuilder<TypeWithAnnotations>.GetInstance();
        ArrayBuilder<RefKind> refs = null;

        for (var param = 0; param < parameters.Length; ++param) {
            var parameter = parameters[param];
            types.Add(parameter.typeWithAnnotations);

            var refKind = parameter.refKind;

            if (refs == null) {
                if (refKind != RefKind.None) {
                    refs = ArrayBuilder<RefKind>.GetInstance(param, RefKind.None);
                    refs.Add(refKind);
                }
            } else {
                refs.Add(refKind);
            }
        }

        var refKinds = refs != null ? refs.ToImmutableAndFree() : default;
        return new ParameterSignature(types.ToImmutableAndFree(), refKinds);
    }

    internal static void PopulateParameterSignature(
        ImmutableArray<ParameterSymbol> parameters,
        ref ParameterSignature lazySignature) {
        if (lazySignature == null)
            Interlocked.CompareExchange(ref lazySignature, MakeParamTypesAndRefKinds(parameters), null);
    }
}
