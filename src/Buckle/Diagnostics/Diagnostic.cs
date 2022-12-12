
namespace Diagnostics;

/// <summary>
/// A message that needs to be tracked about the execution of the program.
/// Usually indicates either an issue, or a warning to be logged or displayed to the user.
/// </summary>
public class Diagnostic {
    /// <summary>
    /// Creates a diagnostic.
    /// </summary>
    /// <param name="info">Severity and code of diagnostic</param>
    /// <param name="message">Message/info on the diagnostic</param>
    /// <param name="suggestion">A possible solution to the problem</param>
    public Diagnostic(
        DiagnosticInfo info, string message, string suggestion) {
        this.info = info;
        this.message = message;
        this.suggestion = suggestion;
    }

    /// <summary>
    /// Creates a diagnostic without a suggestion.
    /// </summary>
    /// <param name="info">Severity and code of diagnostic</param>
    /// <param name="message">Message/info on the diagnostic</param>
    public Diagnostic(DiagnosticInfo info, string message)
        : this(info, message, null) { }

    /// <summary>
    /// Creates a diagnostic with a DiagnosticType instead of DiagnosticInfo (no suggestion).
    /// </summary>
    /// <param name="type">Severity of diagnostic (see DiagnosticType)</param>
    /// <param name="message">Message/info on the diagnostic</param>
    public Diagnostic(DiagnosticType type, string message)
        : this(new DiagnosticInfo(type), message, null) { }

    /// <summary>
    /// Information about the diagnostic including severity, code, and module.
    /// </summary>
    public DiagnosticInfo info { get; }

    /// <summary>
    /// The message given with the diagnostic.
    /// If the diagnostic is shown to the user this is usually the message they see.
    /// </summary>
    public string message { get; }

    /// <summary>
    /// A suggestion message to help guide a possible fix to the problem.
    /// </summary>
    public string suggestion { get; }
}
