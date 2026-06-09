using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Binding;

internal partial class DeconstructionVariablePendingInference {
    private protected override BelteDiagnostic GetTypeInferenceError(TextLocation location, string text) {
        return Error.TypeInferenceFailedForDeconstruction(location, text);
    }
}
