using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class SynthesizedParameterSymbol : SynthesizedParameterSymbolBase {
    private SynthesizedParameterSymbol(
        Symbol container,
        TypeWithAnnotations type,
        int ordinal,
        RefKind refKind,
        ScopedKind scope,
        string name,
        bool isConst)
        : base(container, type, ordinal, refKind, scope, name, isConst) { }

    internal static ParameterSymbol Create(
        Symbol container,
        TypeWithAnnotations type,
        int ordinal,
        RefKind refKind,
        string name = "",
        ScopedKind scope = ScopedKind.None,
        ConstantValue defaultValue = null,
        SourceComplexParameterSymbolBase baseParameter = null,
        bool isConst = false) {
        if (defaultValue is null && baseParameter is null)
            return new SynthesizedParameterSymbol(container, type, ordinal, refKind, scope, name, isConst);

        return new SynthesizedComplexParameterSymbol(
            container,
            type,
            ordinal,
            refKind,
            scope,
            defaultValue,
            name,
            baseParameter,
            isConst
        );
    }

    internal static ImmutableArray<ParameterSymbol> DeriveParameters(
        MethodSymbol sourceMethod,
        MethodSymbol destinationMethod) {
        return sourceMethod.parameters.SelectAsArray(
            static (oldParam, destinationMethod) => DeriveParameter(destinationMethod, oldParam),
            destinationMethod
        );
    }

    internal static ParameterSymbol DeriveParameter(Symbol destination, ParameterSymbol oldParam) {
        return Create(
            destination,
            oldParam.typeWithAnnotations,
            oldParam.ordinal,
            oldParam.refKind,
            oldParam.name,
            oldParam.effectiveScope,
            oldParam.explicitDefaultConstantValue
        );
    }
}
