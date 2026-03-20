using Buckle.CodeAnalysis.Text;

namespace Buckle.Diagnostics;

internal class BelteDivideByZeroException : BelteEvaluatorException {
    internal BelteDivideByZeroException(TextLocation location)
        : base("Attempted to divide by zero.", location) { }
}
