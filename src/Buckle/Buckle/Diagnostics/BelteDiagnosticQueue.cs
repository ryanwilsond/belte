using System.Collections.Generic;
using System.Linq;
using Diagnostics;

namespace Buckle.Diagnostics;

/// <summary>
/// A <see cref="DiagnosticQueue<T>" /> containing <see cref="BelteDiagnostic" />s.
/// </summary>
public sealed class BelteDiagnosticQueue : DiagnosticQueue<BelteDiagnostic> {
    /// <summary>
    /// Creates a <see cref="BelteDiagnosticQueue" /> with no Diagnostics.
    /// </summary>
    public BelteDiagnosticQueue() : base() { }

    /// <summary>
    /// Creates a <see cref="BelteDiagnosticQueue" /> with Diagnostics (ordered from oldest -> newest).
    /// </summary>
    /// <param name="diagnostics">Diagnostics to copy into <see cref="BelteDiagnosticQueue" /> initially.</param>
    public BelteDiagnosticQueue(IEnumerable<BelteDiagnostic> diagnostics) : base(diagnostics) { }

    /// <summary>
    /// Sorts, removes duplicates, and modifies Diagnostics.
    /// </summary>
    /// <param name="diagnostics"><see cref="BelteDiagnosticQueue" /> to copy then clean, does not modify
    /// <see cref="BelteDiagnosticQueue" />.</param>
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

}
