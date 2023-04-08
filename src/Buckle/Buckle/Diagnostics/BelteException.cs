using System;

namespace Buckle.Diagnostics;

/// <summary>
/// Exception base class for all Belte related exceptions. Not meant to be used directly unless necessary, similar to
/// the usage of Exception.
/// </summary>
public abstract class BelteException : Exception {
    protected BelteException() { }

    protected BelteException(string message) : base(message) { }

    protected BelteException(string message, Exception inner) : base(message, inner) { }
}
