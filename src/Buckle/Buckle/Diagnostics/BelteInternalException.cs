using System;

namespace Buckle.Diagnostics;

/// <summary>
/// Belte exception meant to be used for critical errors in the compiler when using Diagnostics is not an option.
/// </summary>
internal sealed class BelteInternalException : BelteException {
    public BelteInternalException() { }

    public BelteInternalException(string message) : base(CreateMessage(message)) { }

    public BelteInternalException(string message, Exception inner) : base(CreateMessage(message), inner) { }

    private static string CreateMessage(string message) {
        var title = Uri.EscapeDataString(message);
        // First 3 traces are from BelteInternalException, 4th one is where the exception was actually thrown
        var trace = string.Join(Environment.NewLine, Environment.StackTrace.Split(Environment.NewLine)[3]);
        var body = Uri.EscapeDataString($@"
**Fatal Exception**

{trace}

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

        return $"BU0000: {message}{Environment.NewLine}{Environment.NewLine}     " +
            $"-- Click the following link to report this issue:{Environment.NewLine}\t" +
            $"https://github.com/ryanwilsond/belte/issues/new?assignees=&labels=&title={title}&body={body}" +
            $"{Environment.NewLine}";
    }
}
