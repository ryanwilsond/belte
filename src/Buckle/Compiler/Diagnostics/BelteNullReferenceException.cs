using Buckle.CodeAnalysis.Text;

namespace Buckle.Diagnostics;

internal class BelteNullReferenceException : BelteEvaluatorException {
    internal BelteNullReferenceException(TextLocation location)
        : base("Object reference not set to an instance of an object.", location) { }
}
