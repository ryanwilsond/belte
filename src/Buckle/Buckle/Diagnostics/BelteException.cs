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

    public BelteInternalException(string message) : base(CreateMessage(message)) { }

    public BelteInternalException(string message, Exception inner) : base(CreateMessage(message), inner) { }

    private static string CreateMessage(string message) {
        // ! This will break if any of the GitHub project structure changes
        // This is a shortcut to the '%20' code for spaces in urls
        var title = message.Replace(' ', '+');

        return $"BU0000: {message}\n\n\tClick the following link to report this issue:\n\t" +
            $"https://github.com/ryanwilsond/belte/issues/new?assignees=&labels=&template=bug_report.md&title={title}" +
            "\n";
    }
}

/// <summary>
/// Belte exception thrown when an evaluating thread should abort evaluation.
/// </summary>
internal sealed class BelteThreadException : BelteException {
    public BelteThreadException() { }

    public BelteThreadException(string message) : base(message) { }

    public BelteThreadException(string message, Exception inner) : base(message, inner) { }
}
