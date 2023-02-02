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
        var title = Uri.EscapeDataString(message);
        var body = Uri.EscapeDataString($@"
**Fatal Exception**

{Environment.StackTrace}

**To Reproduce**
Steps to reproduce the behavior:
1. Go to '...'
2. Click on '....'
3. Scroll down to '....'
4. See error

**Screenshots**
If applicable, add screenshots to help explain your problem.

**Desktop (please complete the following information)**
 - Buckle Version [e.g. 0.1.22]

**Additional context**
Add any other context about the problem here."
        );

        return $"BU0000: {message}\n\n     -- Click the following link to report this issue:\n\t" +
            $"https://github.com/ryanwilsond/belte/issues/new?assignees=&labels=&title={title}&body={body}\n";
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
