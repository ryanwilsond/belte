using System.Diagnostics;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class SynthesizedPropertyAccessorValueParameterSymbol : SynthesizedAccessorValueParameterSymbol {
    internal SynthesizedPropertyAccessorValueParameterSymbol(SourcePropertyAccessorSymbol accessor, int ordinal)
        : base(accessor, ordinal) {
        Debug.Assert(accessor.locations.Length <= 1);
    }

    internal override TypeWithAnnotations typeWithAnnotations
        => ((PropertySymbol)((SourcePropertyAccessorSymbol)containingSymbol).associatedSymbol).typeWithAnnotations;
}
