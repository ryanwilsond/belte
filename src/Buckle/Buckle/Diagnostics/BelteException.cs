using System;

namespace Buckle.Diagnostics;

/// <summary>
/// Exception base class for all Belte related exceptions. Not meant to be used directly unless necessary, similar to
/// the usage of Exception.
/// </summary>
internal class BelteException : Exception {
    public BelteException() { }

    public BelteException(string message) : base(message) { }

    public BelteException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
/// Belte exception meant to be used for critical errors in the compiler when using Diagnostics is not an option.
/// </summary>
internal class BelteInternalException : BelteException {
    public BelteInternalException() { }

    public BelteInternalException(string message) : base(message) { }

    public BelteInternalException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
/// Belte exception meant to be used for exceptions in Belte source code to be shown and fixed by the user.
/// </summary>
internal class BelteLanguageException : BelteException {
    public BelteLanguageException() { }

    public BelteLanguageException(string message) : base(message) { }

    public BelteLanguageException(string message, Exception inner) : base(message, inner) { }
}
