using System;

namespace Buckle.Diagnostics;

/// <summary>
/// Belte exception thrown when an evaluating thread should abort evaluation.
/// </summary>
internal sealed class BelteThreadException : BelteException {
    public BelteThreadException() { }

    public BelteThreadException(string message) : base(message) { }

    public BelteThreadException(string message, Exception inner) : base(message, inner) { }
}
