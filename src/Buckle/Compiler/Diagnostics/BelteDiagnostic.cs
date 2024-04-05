using Buckle.CodeAnalysis.Text;
using Diagnostics;

namespace Buckle.Diagnostics;

/// <summary>
/// Belte/Buckle specific <see cref="Diagnostic" />.
/// </summary>
public sealed class BelteDiagnostic : Diagnostic {
    /// <summary>
    /// Creates a <see cref="BelteDiagnostic" />.
    /// </summary>
    /// <param name="info">Severity and code of <see cref="BelteDiagnostic" />.</param>
    /// <param name="location">Location of the <see cref="BelteDiagnostic" />.</param>
    /// <param name="message">Message/info on the <see cref="BelteDiagnostic" />.</param>
    /// <param name="suggestions">Possible solution(s) to the problem.</param>
    public BelteDiagnostic(DiagnosticInfo info, TextLocation location, string message, string[] suggestions)
        : base(info, message, suggestions) {
        this.location = location;
    }

    /// <summary>
    /// Creates a <see cref="BelteDiagnostic" /> without a suggestion.
    /// </summary>
    /// <param name="info">Severity and code of <see cref="BelteDiagnostic" />.</param>
    /// <param name="location">Location of the <see cref="BelteDiagnostic" />.</param>
    /// <param name="message">Message/info on the <see cref="BelteDiagnostic" />.</param>
    public BelteDiagnostic(DiagnosticInfo info, TextLocation location, string message)
        : this(info, location, message, []) { }

    /// <summary>
    /// Creates a <see cref="BelteDiagnostic" /> using a severity instead of <see cref="DiagnosticInfo" />,
    /// no suggestion.
    /// </summary>
    /// <param name="type">Severity of <see cref="BelteDiagnostic" />.</param>
    /// <param name="location">Location of the <see cref="BelteDiagnostic" />.</param>
    /// <param name="message">Message/info on the <see cref="BelteDiagnostic" />.</param>
    public BelteDiagnostic(DiagnosticSeverity type, TextLocation location, string message)
        : this(new DiagnosticInfo(type), location, message, []) { }

    /// <summary>
    /// Creates a <see cref="BelteDiagnostic" /> using a severity instead of <see cref="DiagnosticInfo" />,
    /// no suggestion, and  no location.
    /// </summary>
    /// <param name="type">Severity of <see cref="BelteDiagnostic" />.</param>
    /// <param name="message">Message/info on the <see cref="BelteDiagnostic" />.</param>
    public BelteDiagnostic(DiagnosticSeverity type, string message)
        : this(new DiagnosticInfo(type), null, message, []) { }

    /// <summary>
    /// Creates a <see cref="BelteDiagnostic" /> without a location or suggestion.
    /// </summary>
    /// <param name="info">Severity and code of <see cref="BelteDiagnostic" />.</param>
    /// <param name="message">Message/info on the <see cref="BelteDiagnostic" />.</param>
    public BelteDiagnostic(DiagnosticInfo info, string message)
        : this(info, null, message, null) { }

    /// <summary>
    /// Creates a <see cref="BelteDiagnostic" /> from an existing <see cref="BelteDiagnostic" /> (copies).
    /// </summary>
    /// <param name="diagnostic"><see cref="BelteDiagnostic" /> to copy (soft copy).</param>
    public BelteDiagnostic(Diagnostic diagnostic)
        : this(diagnostic.info, null, diagnostic.message, diagnostic.suggestions) { }

    /// <summary>
    /// Where the <see cref="BelteDiagnostic" /> is in the source code (what code produced the
    /// <see cref="BelteDiagnostic" />).
    /// </summary>
    public TextLocation location { get; }
}
