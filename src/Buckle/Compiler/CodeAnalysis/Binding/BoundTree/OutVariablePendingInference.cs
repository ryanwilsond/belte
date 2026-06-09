using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Binding;

internal partial class OutVariablePendingInference {
    private protected override BelteDiagnostic GetTypeInferenceError(TextLocation location, string text) {
        return Error.TypeInferenceFailedForOut(location, text);
    }
}
