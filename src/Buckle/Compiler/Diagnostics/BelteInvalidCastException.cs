using Buckle.CodeAnalysis.Text;

namespace Buckle.Diagnostics;

internal class BelteInvalidCastException : BelteEvaluatorException {
    internal BelteInvalidCastException(TextLocation location)
        : base("Specified cast is not valid.", location) { }
}
