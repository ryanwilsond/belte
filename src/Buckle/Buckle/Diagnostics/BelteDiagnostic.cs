using System.Collections.Generic;
using System.Linq;
using Buckle.CodeAnalysis.Text;
using Diagnostics;

namespace Buckle.Diagnostics;

/// <summary>
/// Diagnostic queue for BelteDiagnostics.
/// </summary>
public sealed class BelteDiagnosticQueue : DiagnosticQueue<BelteDiagnostic> {
    /// <summary>
    /// Creates a queue with no diagnostics.
    /// </summary>
    public BelteDiagnosticQueue() : base() { }

    /// <summary>
    /// Creates a queue with diagnostics (ordered from oldest -> newest).
    /// </summary>
    /// <param name="diagnostics">Diagnostics to copy into queue initially</param>
    public BelteDiagnosticQueue(IEnumerable<BelteDiagnostic> diagnostics) : base(diagnostics) { }

    /// <summary>
    /// Sorts, removes duplicates, and modifies diagnostics.
    /// </summary>
    /// <param name="diagnostics">Queue to copy then clean, does not modify queue</param>
    /// <returns>New cleaned queue</returns>
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
    /// Copies queue without a specific severity of diagnostic.
    /// </summary>
    /// <param name="type">Severity to not copy (see DiagnosticType)</param>
    /// <returns>New, unlinked queue</returns>
    public new BelteDiagnosticQueue FilterOut(DiagnosticType type) {
        return new BelteDiagnosticQueue(AsList().Where(d => d.info.severity != type));
    }
}

/// <summary>
/// Belte/Buckle specific diagnostic.
/// </summary>
public sealed class BelteDiagnostic : Diagnostic {
    /// <summary>
    /// Creates a diagnostic.
    /// </summary>
    /// <param name="info">Severity and code of diagnostic</param>
    /// <param name="location">Location of the diagnostic</param>
    /// <param name="message">Message/info on the diagnostic</param>
    /// <param name="suggestion">A possible solution to the problem</param>
    public BelteDiagnostic(DiagnosticInfo info, TextLocation location, string message, string suggestion)
        : base (info, message, suggestion) {
        this.location = location;
    }

    /// <summary>
    /// Creates a diagnostic without a suggestion.
    /// </summary>
    /// <param name="info">Severity and code of diagnostic</param>
    /// <param name="location">Location of the diagnostic</param>
    /// <param name="message">Message/info on the diagnostic</param>
    public BelteDiagnostic(DiagnosticInfo info, TextLocation location, string message)
        : this(info, location, message, null) { }

    /// <summary>
    /// Creates a diagnostic using a severity instead of DiagnosticInfo, no suggestion.
    /// </summary>
    /// <param name="type">Severity of diagnostic</param>
    /// <param name="location">Location of the diagnostic</param>
    /// <param name="message">Message/info on the diagnostic</param>
    public BelteDiagnostic(DiagnosticType type, TextLocation location, string message)
        : this(new DiagnosticInfo(type), location, message, null) { }

    /// <summary>
    /// Creates a diagnostic using a severity instead of DiagnosticInfo, no suggestion, and  no location.
    /// </summary>
    /// <param name="type">Severity of diagnostic</param>
    /// <param name="message">Message/info on the diagnostic</param>
    public BelteDiagnostic(DiagnosticType type, string message)
        : this(new DiagnosticInfo(type), null, message, null) { }

    /// <summary>
    /// Creates a diagnostic without a location or suggestion.
    /// </summary>
    /// <param name="info">Severity and code of diagnostic</param>
    /// <param name="message">Message/info on the diagnostic</param>
    public BelteDiagnostic(DiagnosticInfo info, string message)
        : this(info, null, message, null) { }

    /// <summary>
    /// Creates a diagnostic from an existing diagnostic (copies).
    /// </summary>
    /// <param name="diagnostic">Diagnostic to copy (soft copy)</param>
    public BelteDiagnostic(Diagnostic diagnostic)
        : this(diagnostic.info, null, diagnostic.message, diagnostic.suggestion) { }

    /// <summary>
    /// Where the diagnostic is in the source code (what code produced the diagnostic).
    /// </summary>
    public TextLocation location { get; }
}
