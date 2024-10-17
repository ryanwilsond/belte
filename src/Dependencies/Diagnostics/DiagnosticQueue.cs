using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Diagnostics;

/// <summary>
/// A queue style data structure that handles storing and retrieving many Diagnostics.
/// </summary>
/// <typeparam name="T">The type of <see cref="Diagnostic" /> to store.</typeparam>
public class DiagnosticQueue<T> where T : Diagnostic {
    private readonly List<T> _diagnostics;

    /// <summary>
    /// Creates an empty <see cref="DiagnosticQueue<T>" /> (no Diagnostics)
    /// </summary>
    public DiagnosticQueue() {
        _diagnostics = new List<T>();
    }

    /// <summary>
    /// Creates a <see cref="DiagnosticQueue<T>" /> with items (ordered from oldest -> newest).
    /// </summary>
    /// <param name="diagnostics">Initialize with enumerable (copy).</param>
    public DiagnosticQueue(IEnumerable<T> diagnostics) {
        _diagnostics = diagnostics.ToList();
    }

    /// <summary>
    /// How many Diagnostics are currently stored in the <see cref="DiagnosticQueue<T>" />.
    /// </summary>
    public int Count => _diagnostics.Count;

    /// <summary>
    /// Checks for any Diagnostics in the <see cref="DiagnosticQueue<T>" />.
    /// </summary>
    /// <returns>
    /// True if there are at least 1 <see cref="Diagnostic" /> in the <see cref="DiagnosticQueue<T>" />.
    /// </returns>
    public bool Any() => _diagnostics.Count != 0;

    /// <summary>
    /// Gets an enumerator for the <see cref="DiagnosticQueue<T>" /> collection (sidestepping the queue structure).
    /// </summary>
    /// <returns>The enumerator for the <see cref="DiagnosticQueue<T>" /> as if it were a list.</returns>
    public IEnumerator GetEnumerator() => _diagnostics.GetEnumerator();

    /// <summary>
    /// Converts the <see cref="DiagnosticQueue<T>" /> into an array (ordered from oldest -> newest item added to
    /// <see cref="DiagnosticQueue<T>" />).
    /// </summary>
    /// <returns>Array copy of the <see cref="DiagnosticQueue<T>" /> (not a reference).</returns>
    public Diagnostic[] ToArray() => _diagnostics.ToArray();

    /// <summary>
    /// Checks if any Diagnostics of given type.
    /// </summary>
    /// <param name="type">Type to check for, ignores all other Diagnostics.</param>
    /// <returns>If any Diagnostics of type.</returns>
    public bool Any(DiagnosticSeverity type) {
        return _diagnostics.Where(d => d.info.severity == type).Any();
    }

    /// <summary>
    /// Pushes a <see cref="Diagnostic" /> onto the <see cref="DiagnosticQueue<T>" />.
    /// </summary>
    /// <param name="diagnostic"><see cref="Diagnostic" /> to copy onto the <see cref="DiagnosticQueue<T>" />.</param>
    public DiagnosticInfo Push(T diagnostic) {
        if (diagnostic is not null)
            _diagnostics.Add(diagnostic);

        return diagnostic.info;
    }

    /// <summary>
    /// Pops all Diagnostics off <see cref="DiagnosticQueue<T>" /> and pushes them onto this.
    /// </summary>
    /// <param name="diagnosticQueue"><see cref="DiagnosticQueue<T>" /> to pop and copy from.</param>
    public void Move(DiagnosticQueue<T> diagnosticQueue) {
        if (diagnosticQueue is null)
            return;

        var diagnostic = diagnosticQueue.Pop();

        while (diagnostic is not null) {
            _diagnostics.Add(diagnostic);
            diagnostic = diagnosticQueue.Pop();
        }
    }

    /// <summary>
    /// Removes first <see cref="Diagnostic" />.
    /// </summary>
    /// <returns>First <see cref="Diagnostic" /> on the <see cref="DiagnosticQueue<T>" />.</returns>
    public T? Pop() {
        if (_diagnostics.Count == 0)
            return null;

        var diagnostic = _diagnostics[0];
        _diagnostics.RemoveAt(0);

        return diagnostic;
    }

    /// <summary>
    /// Removes last <see cref="Diagnostic" />.
    /// </summary>
    /// <returns>Last <see cref="Diagnostic" /> on the <see cref="DiagnosticQueue<T>" />.</returns>
    public T? PopBack() {
        if (_diagnostics.Count == 0)
            return null;

        var diagnostic = _diagnostics[^1];
        _diagnostics.RemoveAt(_diagnostics.Count - 1);

        return diagnostic;
    }

    /// <summary>
    /// Removes all Diagnostics.
    /// </summary>
    public void Clear() {
        _diagnostics.Clear();
    }

    /// <summary>
    /// Removes all Diagnostics of a specific severity (see <see cref="DiagnosticSeverity" />).
    /// </summary>
    /// <param name="type">Severity of Diagnostics to remove.</param>
    public void Clear(DiagnosticSeverity type) {
        for (var i = 0; i < _diagnostics.Count; i++) {
            if (_diagnostics[i].info.severity == type)
                _diagnostics.RemoveAt(i--);
        }
    }

    /// <summary>
    /// Returns a list of all the Diagnostics in the <see cref="DiagnosticQueue<T>" /> in order.
    /// </summary>
    /// <returns>List of Diagnostics (ordered oldest -> newest).</returns>
    public List<T> ToList() {
        return _diagnostics;
    }

    /// <summary>
    /// Returns a list of all the Diagnostics in the <see cref="DiagnosticQueue<T>" /> in order, and casts them to a new
    /// <see cref="Diagnostic" /> child type.
    /// </summary>
    /// <typeparam name="NewType">Type of <see cref="Diagnostic" /> to cast existing Diagnostics to.</typeparam>
    /// <returns>List of Diagnostics (ordered oldest -> newest).</returns>
    public List<NewType> ToList<NewType>() where NewType : Diagnostic {
        return _diagnostics as List<NewType>;
    }

    /// <summary>
    /// Returns a new queue only including specific DiagnosticSeverities. Does not affect this instance.
    /// </summary>
    /// <param name="types">Which DiagnosticSeverities to include.</param>
    /// <returns>Filtered queue.</returns>
    public DiagnosticQueue<T> Filter(params DiagnosticSeverity[] severities) {
        return new DiagnosticQueue<T>(_diagnostics.Where(d => severities.Contains(d.info.severity)));
    }

    /// <summary>
    /// Returns a new queue with all Diagnostics above or equal to a specific severity. Does not affect this instance.
    /// </summary>
    /// <param name="severity">Lowest <see cref="DiagnosticSeverity" /> to include.</param>
    /// <returns>Filtered queue.</returns>
    public DiagnosticQueue<T> FilterAbove(DiagnosticSeverity severity) {
        return new DiagnosticQueue<T>(_diagnostics.Where(d => (int)d.info.severity >= (int)severity));
    }

    /// <summary>
    /// Returns a new queue without specific DiagnosticSeverities. Does not affect this instance.
    /// </summary>
    /// <param name="types">Which DiagnosticSeverities to exclude.</param>
    /// <returns>Filtered queue.</returns>
    public DiagnosticQueue<T> FilterOut(params DiagnosticSeverity[] severities) {
        return new DiagnosticQueue<T>(_diagnostics.Where(d => !severities.Contains(d.info.severity)));
    }

    /// <summary>
    /// Copies another <see cref="Diagnostic" /> queue to the front of this <see cref="DiagnosticQueue<T>" />.
    /// </summary>
    /// <param name="queue">
    /// <see cref="DiagnosticQueue<T>" /> to copy, does not modify this <see cref="DiagnosticQueue<T>" />.
    /// </param>
    public void CopyToFront(DiagnosticQueue<T> queue) {
        _diagnostics.InsertRange(0, queue._diagnostics);
    }
}
