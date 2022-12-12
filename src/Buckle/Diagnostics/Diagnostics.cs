using System.Collections;

namespace Diagnostics;

/// <summary>
/// Severity of diagnostic, does not effect how the DiagnosticQueue interacts with them.
/// </summary>
public enum DiagnosticType {
    Error,
    Warning,
    Fatal,
    Unknown,
}

/// <summary>
/// Information about a diagnostic including severity (see DiagnosticType), code, and module.
/// </summary>
public sealed class DiagnosticInfo {
    /// <summary>
    /// Creates an empty DiagnosticInfo (severity is set to DiagnosticType.Unknown).
    /// </summary>
    public DiagnosticInfo() {
        code = null;
        module = null;
        severity = DiagnosticType.Unknown;
    }

    /// <summary>
    /// Creates a new DiagnosticInfo (severity is set to DiagnosticType.Unknown).
    /// </summary>
    /// <param name="code">User defined code for what caused the diagnostic</param>
    /// <param name="module">What module of code produced the diagnostic (user defined)</param>
    public DiagnosticInfo(int code, string module) {
        this.code = code;
        this.module = module;
        this.severity = DiagnosticType.Unknown;
    }

    /// <summary>
    /// Creates am empty DiagnosticInfo with a severity.
    /// </summary>
    /// <param name="severity">Severity of diagnostic (see DiagnosticType)</param>
    public DiagnosticInfo(DiagnosticType severity) {
        code = null;
        this.severity = severity;
    }

    /// <summary>
    /// Creates a new DiagnosticInfo.
    /// </summary>
    /// <param name="code">User defined code for what caused the diagnostic</param>
    /// <param name="module">What module of code produced the diagnostic (user defined)</param>
    /// <param name="severity">Severity of diagnostic (see DiagnosticType)</param>
    public DiagnosticInfo(int code, string module, DiagnosticType severity) {
        this.code = code;
        this.module = module;
        this.severity = severity;
    }

    /// <summary>
    /// The severity of this diagnostic (see DiagnosticType).
    /// </summary>
    public DiagnosticType severity { get; }

    /// <summary>
    /// The user defined code to describe what caused this diagnostic.
    /// </summary>
    public int? code { get; }

    /// <summary>
    /// What module of code produced this diagnostic.
    /// </summary>
    public string module { get; }
}

/// <summary>
/// A message that needs to be tracked about the execution of the program.
/// Usually indicates either an issue, or a warning to be logged or displayed to the user.
/// </summary>
public class Diagnostic {
    /// <summary>
    /// Creates a diagnostic.
    /// </summary>
    /// <param name="info">Severity and code of diagnostic</param>
    /// <param name="message">Message/info on the diagnostic</param>
    /// <param name="suggestion">A possible solution to the problem</param>
    public Diagnostic(
        DiagnosticInfo info, string message, string suggestion) {
        this.info = info;
        this.message = message;
        this.suggestion = suggestion;
    }

    /// <summary>
    /// Creates a diagnostic without a suggestion.
    /// </summary>
    /// <param name="info">Severity and code of diagnostic</param>
    /// <param name="message">Message/info on the diagnostic</param>
    public Diagnostic(DiagnosticInfo info, string message)
        : this(info, message, null) { }

    /// <summary>
    /// Creates a diagnostic with a DiagnosticType instead of DiagnosticInfo (no suggestion).
    /// </summary>
    /// <param name="type">Severity of diagnostic (see DiagnosticType)</param>
    /// <param name="message">Message/info on the diagnostic</param>
    public Diagnostic(DiagnosticType type, string message)
        : this(new DiagnosticInfo(type), message, null) { }

    /// <summary>
    /// Information about the diagnostic including severity, code, and module.
    /// </summary>
    public DiagnosticInfo info { get; }

    /// <summary>
    /// The message given with the diagnostic.
    /// If the diagnostic is shown to the user this is usually the message they see.
    /// </summary>
    public string message { get; }

    /// <summary>
    /// A suggestion message to help guide a possible fix to the problem.
    /// </summary>
    public string suggestion { get; }
}

/// <summary>
/// A queue style data structure that handles storing and retrieving many diagnostics.
/// </summary>
/// <typeparam name="Type">The type of diagnostic to store</typeparam>
public class DiagnosticQueue<Type> where Type : Diagnostic {
    /// <summary>
    /// Diagnostics in queue currently.
    /// Queue is a wrapper to simulate a list, but internal representation of diagnostics is a list.
    /// </summary>
    internal List<Type> _diagnostics;

    /// <summary>
    /// Creates an empty DiagnosticQueue (no diagnostics)
    /// </summary>
    public DiagnosticQueue() {
        _diagnostics = new List<Type>();
    }

    /// <summary>
    /// Creates a DiagnosticQueue with items (ordered from oldest -> newest).
    /// </summary>
    /// <param name="diagnostics">Initialize with enumerable (copy)</param>
    public DiagnosticQueue(IEnumerable<Type> diagnostics) {
        _diagnostics = diagnostics.ToList();
    }

    /// <summary>
    /// How many diagnostics are currently stored in the queue.
    /// </summary>
    public int count => _diagnostics.Count;

    /// <summary>
    /// Checks for any diagnostics in the queue.
    /// </summary>
    /// <returns>True if there are at least 1 diagnostic in the queue</returns>
    public bool Any() => _diagnostics.Any();

    /// <summary>
    /// Gets an enumerator for the queue collection (sidestepping the queue structure).
    /// </summary>
    /// <returns>The enumerator for the queue as if it were a list</returns>
    public IEnumerator GetEnumerator() => _diagnostics.GetEnumerator();

    /// <summary>
    /// Converts the queue into an array (ordered from oldest -> newest item added to queue).
    /// </summary>
    /// <returns>Array copy of the queue (not a reference)</returns>
    public Diagnostic[] ToArray() => _diagnostics.ToArray();

    /// <summary>
    /// Checks if any diagnostics of given type.
    /// </summary>
    /// <param name="type">Type to check for, ignores all other diagnostics</param>
    /// <returns>If any diagnostics of type</returns>
    public bool Any(DiagnosticType type) {
        return _diagnostics.Where(d => d.info.severity == type).Any();
    }

    /// <summary>
    /// Pushes a diagnostic onto the queue.
    /// </summary>
    /// <param name="diagnostic">Diagnostic to copy onto the queue</param>
    public void Push(Type diagnostic) {
        if (diagnostic != null)
            _diagnostics.Add(diagnostic);
    }

    /// <summary>
    /// Pushes a diagnostic to the front of the queue.
    /// </summary>
    /// <param name="diagnostic">Diagnostic to copy onto the queue</param>
    public void PushToFront(Type diagnostic) {
        if (diagnostic != null)
            _diagnostics.Insert(0, diagnostic);
    }

    /// <summary>
    /// Pops all diagnostics off queue and pushes them onto this.
    /// </summary>
    /// <param name="diagnosticQueue">Queue to pop and copy from</param>
    public void Move(DiagnosticQueue<Type> diagnosticQueue) {
        if (diagnosticQueue == null)
            return;

        Type diagnostic = diagnosticQueue.Pop();
        while (diagnostic != null) {
            _diagnostics.Add(diagnostic);
            diagnostic = diagnosticQueue.Pop();
        }
    }

    /// <summary>
    /// Pops all diagnostics off all queues and pushes them onto this.
    /// </summary>
    /// <param name="diagnosticQueues">Queues to pop and copy from</param>
    public void MoveMany(IEnumerable<DiagnosticQueue<Type>> diagnosticQueues) {
        if (diagnosticQueues == null)
            return;

        foreach (var diagnosticQueue in diagnosticQueues)
            Move(diagnosticQueue);
    }

    /// <summary>
    /// Removes first diagnostic.
    /// </summary>
    /// <returns>First diagnostic on the queue</returns>
    public Type? Pop() {
        if (_diagnostics.Count == 0)
            return null;

        Type diagnostic = _diagnostics[0];
        _diagnostics.RemoveAt(0);
        return diagnostic;
    }

    /// <summary>
    /// Removes last diagnostic.
    /// </summary>
    /// <returns>Last diagnostic on the queue</returns>
    public Type? PopBack() {
        if (_diagnostics.Count == 0)
            return null;

        Type diagnostic = _diagnostics[_diagnostics.Count - 1];
        _diagnostics.RemoveAt(_diagnostics.Count - 1);
        return diagnostic;
    }

    /// <summary>
    /// Removes all diagnostics.
    /// </summary>
    public void Clear() {
        _diagnostics.Clear();
    }

    /// <summary>
    /// Removes all diagnostics of a specific severity (see DiagnosticType).
    /// </summary>
    /// <param name="type">Severity of diagnostics to remove</param>
    public void Clear(DiagnosticType type) {
        for (int i=0; i<_diagnostics.Count; i++) {
            if (_diagnostics[i].info.severity == type)
                _diagnostics.RemoveAt(i--);
        }
    }

    /// <summary>
    /// Returns a list of all the diagnostics in the queue in order.
    /// </summary>
    /// <returns>List of diagnostics (ordered oldest -> newest)</returns>
    public List<Type> AsList() {
        return _diagnostics;
    }

    /// <summary>
    /// Returns a list of all the diagnostics in the queue in order, and casts them to a new diagnostic child type.
    /// </summary>
    /// <typeparam name="NewType">Type of diagnostic to cast existing diagnostics to</typeparam>
    /// <returns>List of diagnostics (ordered oldest -> newest)</returns>
    public List<NewType> AsList<NewType>() where NewType : Diagnostic {
        return _diagnostics as List<NewType>;
    }

    /// <summary>
    /// Returns a new queue without a specific type of diagnostic, does not affect this instance.
    /// </summary>
    /// <param name="type">Which diagnostic type to exclude</param>
    /// <returns>New diagnostic queue without any diagnostics of type `type`</returns>
    public DiagnosticQueue<Type> FilterOut(DiagnosticType type) {
        return new DiagnosticQueue<Type>(_diagnostics.Where(d => d.info.severity != type));
    }

    /// <summary>
    /// Copies another diagnostic queue to the front of this queue.
    /// </summary>
    /// <param name="queue">Diagnostic queue to copy, does not modify this queue</param>
    public void CopyToFront(DiagnosticQueue<Type> queue) {
        _diagnostics.InsertRange(0, queue._diagnostics);
    }
}
