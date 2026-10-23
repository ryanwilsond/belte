using Buckle.CodeAnalysis.Text;

namespace Buckle.Diagnostics;

internal class BelteOverflowException : BelteEvaluatorException {
    internal BelteOverflowException(TextLocation location)
        : base("Arithmetic operation resulted in an overflow.", location, failCompileTimeExpressions: false) { }
}
