using Buckle.CodeAnalysis.Text;

namespace Buckle.Diagnostics;

internal class BelteIndexOutOfRangeException : BelteEvaluatorException {
    internal BelteIndexOutOfRangeException(TextLocation location)
        : base("Index was outside the bounds of the array.", location) { }
}
