using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Symbols;

internal class ParameterSignature {
    internal static readonly ParameterSignature NoParams = new ParameterSignature([], default, default);

    internal readonly ImmutableArray<TypeWithAnnotations> parameterTypesWithAnnotations;
    internal readonly ImmutableArray<RefKind> parameterRefKinds;
    internal readonly ImmutableArray<bool> parameterConstnesses;

    private ParameterSignature(
        ImmutableArray<TypeWithAnnotations> parameterTypesWithAnnotations,
        ImmutableArray<RefKind> parameterRefKinds,
        ImmutableArray<bool> parameterConstnesses) {
        this.parameterTypesWithAnnotations = parameterTypesWithAnnotations;
        this.parameterRefKinds = parameterRefKinds;
        this.parameterConstnesses = parameterConstnesses;
    }

    private static ParameterSignature MakeParamTypesAndRefKinds(ImmutableArray<ParameterSymbol> parameters) {
        if (parameters.Length == 0)
            return NoParams;

        var types = ArrayBuilder<TypeWithAnnotations>.GetInstance();
        ArrayBuilder<RefKind> refs = null;
        ArrayBuilder<bool> consts = null;

        for (var param = 0; param < parameters.Length; param++) {
            var parameter = parameters[param];
            types.Add(parameter.typeWithAnnotations);

            var refKind = parameter.refKind;

            if (refs is null) {
                if (refKind != RefKind.None) {
                    refs = ArrayBuilder<RefKind>.GetInstance(param, RefKind.None);
                    refs.Add(refKind);
                }
            } else {
                refs.Add(refKind);
            }

            var constness = parameter.isConst;

            if (consts is null) {
                if (constness) {
                    consts = ArrayBuilder<bool>.GetInstance(param, false);
                    consts.Add(constness);
                }
            } else {
                consts.Add(constness);
            }
        }

        var refKinds = refs is not null ? refs.ToImmutableAndFree() : default;
        var constnesses = consts is not null ? consts.ToImmutableAndFree() : default;
        return new ParameterSignature(types.ToImmutableAndFree(), refKinds, constnesses);
    }

    internal static void PopulateParameterSignature(
        ImmutableArray<ParameterSymbol> parameters,
        ref ParameterSignature lazySignature) {
        if (lazySignature is null)
            Interlocked.CompareExchange(ref lazySignature, MakeParamTypesAndRefKinds(parameters), null);
    }
}
