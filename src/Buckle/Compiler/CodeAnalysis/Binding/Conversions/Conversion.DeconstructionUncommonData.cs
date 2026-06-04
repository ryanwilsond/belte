using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Binding;

internal readonly partial struct Conversion {
    private sealed class DeconstructionUncommonData : UncommonData {
        internal readonly DeconstructMethodInfo deconstructMethodInfo;
        internal readonly ImmutableArray<(BoundValuePlaceholder placeholder, BoundExpression conversion)> deconstructConversionInfo;

        internal DeconstructionUncommonData(
            DeconstructMethodInfo deconstructMethodInfoOpt,
            ImmutableArray<(BoundValuePlaceholder placeholder, BoundExpression conversion)> deconstructConversionInfo) {
            deconstructMethodInfo = deconstructMethodInfoOpt;
            this.deconstructConversionInfo = deconstructConversionInfo;
        }
    }
}
