using System;

namespace Buckle.Diagnostics;

/// <summary>
/// Exception base class for all Belte related exceptions. Not meant to be used directly unless necessary, similar to
/// the usage of Exception.
/// </summary>
public abstract class BelteException : Exception {
    public BelteException() { }

    public BelteException(string message) : base(message) { }

    public BelteException(string message, Exception inner) : base(message, inner) { }
}
