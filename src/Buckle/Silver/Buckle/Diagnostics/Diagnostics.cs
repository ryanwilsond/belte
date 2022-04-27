using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using Buckle.CodeAnalysis.Text;

namespace Buckle.Diagnostics;

public enum DiagnosticType {
    Error,
    Warning,
    Fatal,
    Unknown,
}

public sealed class Diagnostic {
    public DiagnosticType type { get; }
    public string msg { get; }
    public TextLocation location { get; }
    public string suggestion { get; }

    /// <summary>
    /// A diagnostic message with a specific location
    /// </summary>
    /// <param name="type_">Severity of diagnostic</param>
    /// <param name="span_">Location of the diagnostic</param>
    /// <param name="msg_">Message/info on the diagnostic</param>
    public Diagnostic(DiagnosticType type_, TextLocation location_, string msg_, string suggestion_) {
        type = type_;
        msg = msg_;
        location = location_;
        suggestion = suggestion_;
    }

    public Diagnostic(DiagnosticType type, TextLocation location, string msg) : this(type, location, msg, null) { }
}

public sealed class DiagnosticQueue {
    internal List<Diagnostic> diagnostics_;
    public int count => diagnostics_.Count;
    public bool Any() => diagnostics_.Any();

    public IEnumerator GetEnumerator() => diagnostics_.GetEnumerator();
    public Diagnostic[] ToArray() => diagnostics_.ToArray();
    internal void RemoveAt(int index) => diagnostics_.RemoveAt(index);

    /// <summary>
    /// Queue structure for organizing diagnostics
    /// </summary>
    public DiagnosticQueue() {
        diagnostics_ = new List<Diagnostic>();
    }

    /// <param name="diagnostics">Initialize with enumerable</param>
    public DiagnosticQueue(IEnumerable<Diagnostic> diagnostics) {
        diagnostics_ = diagnostics.ToList();
    }

    /// <summary>
    /// Checks if any diagnostics of given type
    /// </summary>
    /// <param name="type">Type to check for, ignores all other diagnostics</param>
    /// <returns>If any diagnostics of type</returns>
    public bool Any(DiagnosticType type) {
        return diagnostics_.Where(d => d.type == type).Any();
    }

    /// <summary>
    /// Pushes a diagnostic onto the Queue
    /// </summary>
    /// <param name="diagnostic">Diagnostic to copy onto the queue</param>
    public void Push(Diagnostic diagnostic) {
        if (diagnostic != null)
            diagnostics_.Add(diagnostic);
    }

    public void Push(DiagnosticType type, TextLocation location, string msg) {
        Push(new Diagnostic(type, location, msg));
    }

    public void Push(TextLocation location, string msg) { Push(DiagnosticType.Error, location, msg); }
    public void Push(DiagnosticType type, string msg) { Push(type, null, msg); }
    public void Push(string msg) { Push(DiagnosticType.Error, null, msg); }

    /// <summary>
    /// Pops all diagnostics off queue and pushes them onto this
    /// </summary>
    /// <param name="diagnosticQueue">Queue to pop and copy from</param>
    public void Move(DiagnosticQueue diagnosticQueue) {
        if (diagnosticQueue == null)
            return;

        Diagnostic diagnostic = diagnosticQueue.Pop();
        while (diagnostic != null) {
            diagnostics_.Add(diagnostic);
            diagnostic = diagnosticQueue.Pop();
        }
    }

    /// <summary>
    /// Pops all diagnostics off all queues and pushes them onto this
    /// </summary>
    /// <param name="diagnosticQueues">Queues to pop and copy from</param>
    public void MoveMany(IEnumerable<DiagnosticQueue> diagnosticQueues) {
        if (diagnosticQueues == null)
            return;

        foreach (var diagnosticQueue in diagnosticQueues)
            Move(diagnosticQueue);
    }

    /// <summary>
    /// Sorts, removes duplicates, and modifies diagnostics
    /// </summary>
    /// <param name="diagnostics">Queue to copy then clean, doesn't modify queue</param>
    /// <returns>New cleaned queue</returns>
    public static DiagnosticQueue CleanDiagnostics(DiagnosticQueue diagnostics) {
        var cleanedDiagnostics = new DiagnosticQueue();

        foreach (var diagnostic in diagnostics.diagnostics_.OrderBy(diag => diag.location.fileName)
                .ThenBy(diag => diag.location.span.start)
                .ThenBy(diag => diag.location.span.length)) {
            cleanedDiagnostics.Push(diagnostic);
        }

        return cleanedDiagnostics;
    }

    /// <summary>
    /// Removes a diagnostic
    /// </summary>
    /// <returns>First diagnostic on the queue</returns>
    public Diagnostic? Pop() {
        if (diagnostics_.Count == 0)
            return null;

        Diagnostic diagnostic = diagnostics_[0];
        diagnostics_.RemoveAt(0);
        return diagnostic;
    }

    /// <summary>
    /// Removes all diagnostics, or all of specific type
    /// </summary>
    public void Clear() {
        diagnostics_.Clear();
    }

    public void Clear(DiagnosticType type) {
        for (int i=0; i<diagnostics_.Count; i++) {
            if (diagnostics_[i].type == type)
                diagnostics_.RemoveAt(i--);
        }
    }

    public DiagnosticQueue FilterOut(DiagnosticType type) {
        return new DiagnosticQueue(diagnostics_.Where(d => d.type != type));
    }
}
