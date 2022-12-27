using System;

namespace Buckle.Diagnostics;

/// <summary>
/// Exception base class for all Belte related exceptions. Not meant to be used directly unless necessary, similar to
/// the usage of Exception.
/// </summary>
internal abstract class BelteException : Exception {
    public BelteException() { }

    public BelteException(string message) : base(message) { }

    public BelteException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
/// Belte exception meant to be used for critical errors in the compiler when using Diagnostics is not an option.
/// </summary>
internal sealed class BelteInternalException : BelteException {
    public BelteInternalException() { }

    public BelteInternalException(string message) : base(message) { }

    public BelteInternalException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
/// Belte exception thrown when an evaluating thread should abort evaluation.
/// </summary>
internal sealed class BelteThreadException : BelteException {
    public BelteThreadException() { }

    public BelteThreadException(string message) : base(message) { }

    public BelteThreadException(string message, Exception inner) : base(message, inner) { }
}
