using System.Collections;

namespace Diagnostics;

public enum DiagnosticType {
    Error,
    Warning,
    Fatal,
    Unknown,
}

public sealed class DiagnosticInfo {
    public DiagnosticType severity { get; }
    public int? code { get; }

    public DiagnosticInfo() {
        code = null;
        severity = DiagnosticType.Unknown;
    }

    public DiagnosticInfo(int code_) {
        code = code_;
        severity = DiagnosticType.Unknown;
    }

    public DiagnosticInfo(DiagnosticType severity_) {
        code = null;
        severity = severity_;
    }

    public DiagnosticInfo(int code_, DiagnosticType severity_) {
        code = code_;
        severity = severity_;
    }
}

public class Diagnostic {
    public DiagnosticInfo info { get; }
    public string message { get; }
    public string suggestion { get; }

    /// <summary>
    /// A diagnostic message with a specific location
    /// </summary>
    /// <param name="info_">Severity and code of diagnostic</param>
    /// <param name="message_">Message/info on the diagnostic</param>
    /// <param name="suggestion_">A possible solution to the problem</param>
    public Diagnostic(
        DiagnosticInfo info_, string message_, string suggestion_) {
        info = info_;
        message = message_;
        suggestion = suggestion_;
    }

    public Diagnostic(DiagnosticInfo info, string message)
        : this(info, message, null) { }

    public Diagnostic(DiagnosticType type, string message)
        : this(new DiagnosticInfo(type), message, null) { }
}

public class DiagnosticQueue<Type> where Type : Diagnostic {
    internal List<Type> diagnostics_;
    public int count => diagnostics_.Count;
    public bool Any() => diagnostics_.Any();

    public IEnumerator GetEnumerator() => diagnostics_.GetEnumerator();
    public Diagnostic[] ToArray() => diagnostics_.ToArray();

    /// <summary>
    /// Queue structure for organizing diagnostics
    /// </summary>
    public DiagnosticQueue() {
        diagnostics_ = new List<Type>();
    }

    /// <param name="diagnostics">Initialize with enumerable</param>
    public DiagnosticQueue(IEnumerable<Type> diagnostics) {
        diagnostics_ = diagnostics.ToList();
    }

    /// <summary>
    /// Checks if any diagnostics of given type
    /// </summary>
    /// <param name="type">Type to check for, ignores all other diagnostics</param>
    /// <returns>If any diagnostics of type</returns>
    public bool Any(DiagnosticType type) {
        return diagnostics_.Where(d => d.info.severity == type).Any();
    }

    /// <summary>
    /// Pushes a diagnostic onto the Queue
    /// </summary>
    /// <param name="diagnostic">Diagnostic to copy onto the queue</param>
    public void Push(Type diagnostic) {
        if (diagnostic != null)
            diagnostics_.Add(diagnostic);
    }

    /// <summary>
    /// Pops all diagnostics off queue and pushes them onto this
    /// </summary>
    /// <param name="diagnosticQueue">Queue to pop and copy from</param>
    public void Move(DiagnosticQueue<Type> diagnosticQueue) {
        if (diagnosticQueue == null)
            return;

        Type diagnostic = diagnosticQueue.Pop();
        while (diagnostic != null) {
            diagnostics_.Add(diagnostic);
            diagnostic = diagnosticQueue.Pop();
        }
    }

    /// <summary>
    /// Pops all diagnostics off all queues and pushes them onto this
    /// </summary>
    /// <param name="diagnosticQueues">Queues to pop and copy from</param>
    public void MoveMany(IEnumerable<DiagnosticQueue<Type>> diagnosticQueues) {
        if (diagnosticQueues == null)
            return;

        foreach (var diagnosticQueue in diagnosticQueues)
            Move(diagnosticQueue);
    }

    /// <summary>
    /// Removes a diagnostic
    /// </summary>
    /// <returns>First diagnostic on the queue</returns>
    public Type? Pop() {
        if (diagnostics_.Count == 0)
            return null;

        Type diagnostic = diagnostics_[0];
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
            if (diagnostics_[i].info.severity == type)
                diagnostics_.RemoveAt(i--);
        }
    }

    /// <summary>
    /// Returns a list of all the diagnostics in the queue in order
    /// Can optionally cast all diagnostics to a child class of diagnostic
    /// </summary>
    /// <returns>List of diagnostics</returns>
    public List<Type> AsList() {
        return diagnostics_;
    }

    public List<NewType> AsList<NewType>() where NewType : Diagnostic {
        return diagnostics_ as List<NewType>;
    }

    /// <summary>
    /// Returns a new queue without a specific type of diagnostic, does not affect this instance
    /// </summary>
    /// <param name="type">Which diagnostic type to exclude</param>
    /// <returns>New diagnostic queue without any diagnostics of type `type`</returns>
    public DiagnosticQueue<Type> FilterOut(DiagnosticType type) {
        return new DiagnosticQueue<Type>(diagnostics_.Where(d => d.info.severity != type));
    }

    /// <summary>
    /// Copies another diagnostic queue to the front of this queue
    /// </summary>
    /// <param name="queue">Diagnostic queue to copy, does not modify this queue</param>
    public void CopyToFront(DiagnosticQueue<Type> queue) {
        diagnostics_.InsertRange(0, queue.diagnostics_);
    }
}
