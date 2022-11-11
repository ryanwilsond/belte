using System.Collections.Generic;
using System.Linq;
using Buckle.CodeAnalysis.Text;
using Diagnostics;

namespace Buckle.Diagnostics;

public sealed class BelteDiagnosticQueue : DiagnosticQueue<BelteDiagnostic> {

    public BelteDiagnosticQueue() : base() { }
    public BelteDiagnosticQueue(IEnumerable<BelteDiagnostic> diagnostics) : base(diagnostics) { }

    /// <summary>
    /// Sorts, removes duplicates, and modifies diagnostics
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

    public new BelteDiagnosticQueue FilterOut(DiagnosticType type) {
        return new BelteDiagnosticQueue(AsList().Where(d => d.info.severity != type));
    }
}

public sealed class BelteDiagnostic : Diagnostic {
    public TextLocation location { get; }

    /// <summary>
    /// A diagnostic message with a specific location
    /// </summary>
    /// <param name="info">Severity and code of diagnostic</param>
    /// <param name="location_">Location of the diagnostic</param>
    /// <param name="message">Message/info on the diagnostic</param>
    /// <param name="suggestion">A possible solution to the problem</param>
    public BelteDiagnostic(DiagnosticInfo info, TextLocation location_, string message, string suggestion)
        : base (info, message, suggestion) {
        location = location_;
    }

    public BelteDiagnostic(DiagnosticInfo info, TextLocation location, string message)
        : this(info, location, message, null) { }

    public BelteDiagnostic(DiagnosticType type, TextLocation location, string message)
        : this(new DiagnosticInfo(type), location, message, null) { }

    public BelteDiagnostic(DiagnosticType type, string message)
        : this(new DiagnosticInfo(type), null, message, null) { }

    public BelteDiagnostic(DiagnosticInfo info, string message)
        : this(info, null, message, null) { }

    public BelteDiagnostic(Diagnostic diagnostic)
        : this(diagnostic.info, null, diagnostic.message, diagnostic.suggestion) { }
}
