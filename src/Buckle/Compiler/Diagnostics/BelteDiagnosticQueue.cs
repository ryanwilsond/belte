using System.Collections.Generic;
using System.Linq;
using Diagnostics;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.Diagnostics;

/// <summary>
/// A <see cref="DiagnosticQueue<T>" /> containing <see cref="BelteDiagnostic" />s.
/// </summary>
public sealed class BelteDiagnosticQueue : DiagnosticQueue<BelteDiagnostic> {
    private static readonly ObjectPool<BelteDiagnosticQueue> Pool
        = new ObjectPool<BelteDiagnosticQueue>(() => new BelteDiagnosticQueue(Pool));

    private readonly ObjectPool<BelteDiagnosticQueue> _pool;

    private BelteDiagnosticQueue(ObjectPool<BelteDiagnosticQueue> pool) : base() {
        _pool = pool;
    }

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
        // TODO This needs to be tested with duplicate diagnostics at the end of the input before being used
        var cleanedDiagnostics = new BelteDiagnosticQueue();
        var specialDiagnostics = new BelteDiagnosticQueue();

        var diagnosticList = diagnostics.ToList<BelteDiagnostic>();

        for (var i = 0; i < diagnosticList.Count; i++) {
            var diagnostic = diagnosticList[i];

            if (diagnostic.location is null) {
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
    /// Filters out any non-error diagnostics. Does not affect this.
    /// </summary>
    /// <returns>Filtered queue.</returns>
    public BelteDiagnosticQueue Errors() {
        return new BelteDiagnosticQueue(FilterAbove(DiagnosticSeverity.Error).ToList());
    }

    public DiagnosticInfo Push<T>(T diagnostic) where T : Diagnostic {
        return base.Push(new BelteDiagnostic(diagnostic));
    }

    public new DiagnosticInfo Push(BelteDiagnostic diagnostic) {
        return base.Push(diagnostic);
    }

    internal static BelteDiagnosticQueue GetInstance() {
        return Pool.Allocate();
    }

    internal void Free() {
        if (_pool is not null) {
            Clear();
            _pool.Free(this);
        }
    }
}
