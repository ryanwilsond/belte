using System.Collections.Generic;
using System.Linq;
using Buckle.CodeAnalysis.Text;
using Diagnostics;

namespace Buckle.Diagnostics;

/// <summary>
/// A <see cref="DiagnosticQueue" /> containing <see cref="BelteDiagnostic" />s.
/// </summary>
public sealed class BelteDiagnosticQueue : DiagnosticQueue<BelteDiagnostic> {
    /// <summary>
    /// Creates a <see cref="BelteDiagnosticQueue" /> with no diagnostics.
    /// </summary>
    public BelteDiagnosticQueue() : base() { }

    /// <summary>
    /// Creates a <see cref="BelteDiagnosticQueue" /> with diagnostics (ordered from oldest -> newest).
    /// </summary>
    /// <param name="diagnostics">Diagnostics to copy into <see cref="BelteDiagnosticQueue" /> initially.</param>
    public BelteDiagnosticQueue(IEnumerable<BelteDiagnostic> diagnostics) : base(diagnostics) { }

    /// <summary>
    /// Sorts, removes duplicates, and modifies diagnostics.
    /// </summary>
    /// <param name="diagnostics"><see cref="BelteDiagnosticQueue" /> to copy then clean, does not modify queue.</param>
    /// <returns>New cleaned <see cref="BelteDiagnosticQueue" />.</returns>
    public static BelteDiagnosticQueue CleanDiagnostics(BelteDiagnosticQueue diagnostics) {
        var cleanedDiagnostics = new BelteDiagnosticQueue();
        var specialDiagnostics = new BelteDiagnosticQueue();

        var diagnosticList = diagnostics.AsList<BelteDiagnostic>();

        for (int i=0; i<diagnosticList.Count; i++) {
            var diagnostic = diagnosticList[i];

            if (diagnostic.location == null) {
                specialDiagnostics.Push(diagnostic);
                diagnosticList.RemoveAt(i--);
            }
        }

        foreach (var diagnostic in diagnosticList.OrderBy(diag => diag.location.fileName)
                .ThenBy(diag => diag.location.span.start)
                .ThenBy(diag => diag.location.span.length)) {
            cleanedDiagnostics.Push(diagnostic);
        }

        cleanedDiagnostics.Move(specialDiagnostics);
        return cleanedDiagnostics;
    }

    /// <summary>
    /// Copies <see cref="BelteDiagnosticQueue" /> without a specific severity of diagnostic.
    /// </summary>
    /// <param name="type">Severity to not copy (see <see cref="DiagnosticType" />).</param>
    /// <returns>New, unlinked <see cref="BelteDiagnosticQueue" />.</returns>
    public new BelteDiagnosticQueue FilterOut(DiagnosticType type) {
        return new BelteDiagnosticQueue(AsList().Where(d => d.info.severity != type));
    }
}

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
    /// <param name="suggestion">A possible solution to the problem.</param>
    public BelteDiagnostic(DiagnosticInfo info, TextLocation location, string message, string suggestion)
        : base (info, message, suggestion) {
        this.location = location;
    }

    /// <summary>
    /// Creates a <see cref="BelteDiagnostic" /> without a suggestion.
    /// </summary>
    /// <param name="info">Severity and code of <see cref="BelteDiagnostic" />.</param>
    /// <param name="location">Location of the <see cref="BelteDiagnostic" />.</param>
    /// <param name="message">Message/info on the <see cref="BelteDiagnostic" />.</param>
    public BelteDiagnostic(DiagnosticInfo info, TextLocation location, string message)
        : this(info, location, message, null) { }

    /// <summary>
    /// Creates a <see cref="BelteDiagnostic" /> using a severity instead of <see cref="DiagnosticInfo" />, no suggestion.
    /// </summary>
    /// <param name="type">Severity of <see cref="BelteDiagnostic" />.</param>
    /// <param name="location">Location of the <see cref="BelteDiagnostic" />.</param>
    /// <param name="message">Message/info on the <see cref="BelteDiagnostic" />.</param>
    public BelteDiagnostic(DiagnosticType type, TextLocation location, string message)
        : this(new DiagnosticInfo(type), location, message, null) { }

    /// <summary>
    /// Creates a <see cref="BelteDiagnostic" /> using a severity instead of <see cref="DiagnosticInfo" />, no suggestion, and  no location.
    /// </summary>
    /// <param name="type">Severity of <see cref="BelteDiagnostic" />.</param>
    /// <param name="message">Message/info on the <see cref="BelteDiagnostic" />.</param>
    public BelteDiagnostic(DiagnosticType type, string message)
        : this(new DiagnosticInfo(type), null, message, null) { }

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
        : this(diagnostic.info, null, diagnostic.message, diagnostic.suggestion) { }

    /// <summary>
    /// Where the <see cref="BelteDiagnostic" /> is in the source code (what code produced the <see cref="BelteDiagnostic" />).
    /// </summary>
    public TextLocation location { get; }
}
