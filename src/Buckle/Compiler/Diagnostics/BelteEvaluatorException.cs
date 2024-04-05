using System;

namespace Buckle.Diagnostics;

/// <summary>
/// Belte exception thrown when an evaluation cannot be performed, even if it is error-free.
/// </summary>
internal sealed class BelteEvaluatorException : BelteException {
    public BelteEvaluatorException() { }

    public BelteEvaluatorException(string message) : base(message) { }

    public BelteEvaluatorException(string message, Exception inner) : base(message, inner) { }
}
