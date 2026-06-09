using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Binding;

internal readonly struct DeconstructMethodInfo {
    internal readonly BoundExpression call;
    internal readonly BoundValuePlaceholder inputPlaceholder;
    internal readonly ImmutableArray<BoundValuePlaceholder> outputPlaceholders;

    internal DeconstructMethodInfo(
        BoundExpression invocation,
        BoundValuePlaceholder inputPlaceholder,
        ImmutableArray<BoundValuePlaceholder> outputPlaceholders) {
        (call, this.inputPlaceholder, this.outputPlaceholders) = (invocation, inputPlaceholder, outputPlaceholders);
    }

    internal bool isDefault => call is null;
}
