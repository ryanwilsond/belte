using Buckle.CodeAnalysis.Text;

namespace Buckle.Diagnostics;

/// <summary>
/// Belte exception thrown when an evaluation cannot be performed, even if it is error-free.
/// </summary>
internal class BelteEvaluatorException : BelteException {
    internal BelteEvaluatorException(string message, TextLocation location, bool failCompileTimeExpressions = true)
        : base(message) {
        this.location = location;
        this.failCompileTimeExpressions = failCompileTimeExpressions;
    }

    internal TextLocation location { get; }

    internal bool failCompileTimeExpressions { get; }
}
