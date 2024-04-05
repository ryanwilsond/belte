
namespace Diagnostics;

/// <summary>
/// A message that needs to be tracked about the execution of the program.
/// Usually indicates either an issue, or a warning to be logged or displayed to the user.
/// </summary>
public class Diagnostic {
    /// <summary>
    /// Creates a <see cref="Diagnostic" />.
    /// </summary>
    /// <param name="info">Severity and code of <see cref="Diagnostic" />.</param>
    /// <param name="message">Message/info on the <see cref="Diagnostic" />.</param>
    /// <param name="suggestions">Possible solution(s) to the problem.</param>
    public Diagnostic(DiagnosticInfo info, string message, string[] suggestions) {
        this.info = info;
        this.message = message;
        this.suggestions = suggestions;
    }

    /// <summary>
    /// Creates a <see cref="Diagnostic" /> without a suggestion.
    /// </summary>
    /// <param name="info">Severity and code of <see cref="Diagnostic" />.</param>
    /// <param name="message">Message/info on the <see cref="Diagnostic" />.</param>
    public Diagnostic(DiagnosticInfo info, string message)
        : this(info, message, []) { }

    /// <summary>
    /// Creates a <see cref="Diagnostic" /> with a <see cref="DiagnosticSeverity" /> instead of
    /// <see cref="DiagnosticInfo" /> (no suggestion).
    /// </summary>
    /// <param name="type">Severity of <see cref="Diagnostic" /> (see <see cref="DiagnosticSeverity" />).</param>
    /// <param name="message">Message/info on the <see cref="Diagnostic" />.</param>
    public Diagnostic(DiagnosticSeverity type, string message)
        : this(new DiagnosticInfo(type), message, []) { }

    /// <summary>
    /// Information about the <see cref="Diagnostic" /> including severity, code, and module.
    /// </summary>
    public DiagnosticInfo info { get; }

    /// <summary>
    /// The message given with the <see cref="Diagnostic" />.
    /// If the <see cref="Diagnostic" /> is shown to the user this is usually the message they see.
    /// </summary>
    public string message { get; }

    /// <summary>
    /// Suggestion message(s) to help guide a possible fix to the problem.
    /// </summary>
    public string[] suggestions { get; }
}
