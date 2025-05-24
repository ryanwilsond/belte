using System;
using Buckle.CodeAnalysis.Text;

namespace Buckle.Diagnostics;

/// <summary>
/// Belte exception thrown when an evaluation cannot be performed, even if it is error-free.
/// </summary>
internal class BelteEvaluatorException : BelteException {
    public BelteEvaluatorException(TextLocation location) {
        this.location = location;
    }

    public BelteEvaluatorException(string message, TextLocation location) : base(message) {
        this.location = location;
    }

    public BelteEvaluatorException(string message, Exception inner, TextLocation location) : base(message, inner) {
        this.location = location;
    }

    public TextLocation location { get; }
}
