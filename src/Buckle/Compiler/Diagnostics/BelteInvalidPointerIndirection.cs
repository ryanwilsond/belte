using Buckle.CodeAnalysis.Text;

namespace Buckle.Diagnostics;

internal class BelteInvalidPointerIndirection : BelteEvaluatorException {
    internal BelteInvalidPointerIndirection(TextLocation location)
        : base("Attempted to read protected memory.", location) { }
}
