using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using Buckle.CodeAnalysis.Symbols;
using Diagnostics;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.Diagnostics;

/// <summary>
/// A <see cref="DiagnosticQueue<T>" /> containing <see cref="BelteDiagnostic" />s.
/// </summary>
[DebuggerDisplay("{GetDebuggerDisplay(), nq}")]
public partial class BelteDiagnosticQueue : DiagnosticQueue<BelteDiagnostic> {
    internal static readonly BelteDiagnosticQueue Discarded = new DiscardedDiagnosticQueue();

    private static readonly ObjectPool<BelteDiagnosticQueue> Pool
        = new ObjectPool<BelteDiagnosticQueue>(() => new BelteDiagnosticQueue(Pool));

    internal readonly ICollection<AssemblySymbol> dependenciesBag;

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
        var specialDiagnostics = GetInstance();

        var diagnosticList = diagnostics.ToArray().ToList();

        for (var i = 0; i < diagnosticList.Count; i++) {
            var diagnostic = diagnosticList[i];

            if (diagnostic.location?.span is null) {
                specialDiagnostics.Push(diagnostic);
                diagnosticList.RemoveAt(i--);
            }
        }

        foreach (var diagnostic in diagnosticList.OrderBy(diag => diag.location.fileName)
                .ThenBy(diag => diag.location.span.start)
                .ThenBy(diag => diag.location.span.length)) {
            cleanedDiagnostics.Push(diagnostic);
        }

        cleanedDiagnostics.PushRange(specialDiagnostics);
        specialDiagnostics.Free();

        return cleanedDiagnostics;
    }

    /// <summary>
    /// Filters out any non-error diagnostics. Does not affect this.
    /// </summary>
    /// <returns>Filtered queue.</returns>
    public BelteDiagnosticQueue Errors() {
        return new BelteDiagnosticQueue(FilterAbove(DiagnosticSeverity.Error).ToList());
    }

    public bool AnyErrors() {
        return AnyAbove(DiagnosticSeverity.Error);
    }

    public virtual DiagnosticInfo Push<T>(T diagnostic) where T : Diagnostic {
        return base.Push(new BelteDiagnostic(diagnostic));
    }

    public new virtual DiagnosticInfo Push(BelteDiagnostic diagnostic) {
        return base.Push(diagnostic);
    }

    public new virtual void PushRange(IEnumerable<BelteDiagnostic> diagnostics) {
        base.PushRange(diagnostics);
    }

    public virtual void PushRange(BelteDiagnosticQueue diagnostics) {
        base.PushRange(diagnostics);
    }

    public virtual void Move(BelteDiagnosticQueue diagnostics) {
        base.Move(diagnostics);
    }

    internal static BelteDiagnosticQueue GetInstance() {
        return Pool.Allocate();
    }

    internal void Free() {
        if (_pool is not null) {
            Clear();
            ((PooledHashSet<AssemblySymbol>)dependenciesBag)?.Free();
            _pool.Free(this);
        }
    }

    internal BelteDiagnostic[] ToArrayAndFree() {
        var diagnostics = ToArray();
        Free();
        return diagnostics;
    }

    internal virtual void PushRangeAndFree(BelteDiagnosticQueue diagnostics) {
        PushRange(diagnostics);
        diagnostics.Free();
    }

    internal ImmutableArray<BelteDiagnostic> ToImmutableAndFree() {
        return ToArrayAndFree().ToImmutableArray();
    }

    internal void AddAssembliesUsedByNamespaceReference(NamespaceSymbol ns) {
        if (dependenciesBag is null)
            return;

        AddAssembliesUsedByNamespaceReferenceImpl(ns);

        void AddAssembliesUsedByNamespaceReferenceImpl(NamespaceSymbol ns) {
            if (ns.extent.kind == NamespaceKind.Compilation) {
                foreach (var constituent in ns.constituentNamespaces)
                    AddAssembliesUsedByNamespaceReferenceImpl(constituent);
            } else {
                var containingAssembly = ns.containingAssembly;

                if (containingAssembly?.isMissing == false)
                    dependenciesBag.Add(containingAssembly);
            }
        }
    }

    private string GetDebuggerDisplay() {
        return "Count = " + (_diagnostics?.Count ?? 0);
    }
}
