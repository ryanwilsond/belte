using System.Collections;

namespace Diagnostics;

/// <summary>
/// A queue style data structure that handles storing and retrieving many Diagnostics.
/// </summary>
/// <typeparam name="T">The type of <see cref="Diagnostic" /> to store.</typeparam>
public class DiagnosticQueue<T> where T : Diagnostic {
    /// <summary>
    /// Diagnostics in queue currently.
    /// <see cref="DiagnosticQueue" /> is a wrapper to simulate a list, but internal representation of Diagnostics
    /// is a list.
    /// </summary>
    internal List<T> _diagnostics;

    /// <summary>
    /// Creates an empty <see cref="DiagnosticQueue" /> (no Diagnostics)
    /// </summary>
    public DiagnosticQueue() {
        _diagnostics = new List<T>();
    }

    /// <summary>
    /// Creates a <see cref="DiagnosticQueue" /> with items (ordered from oldest -> newest).
    /// </summary>
    /// <param name="diagnostics">Initialize with enumerable (copy).</param>
    public DiagnosticQueue(IEnumerable<T> diagnostics) {
        _diagnostics = diagnostics.ToList();
    }

    /// <summary>
    /// How many Diagnostics are currently stored in the <see cref="DiagnosticQueue" />.
    /// </summary>
    public int count => _diagnostics.Count;

    /// <summary>
    /// Checks for any Diagnostics in the <see cref="DiagnosticQueue" />.
    /// </summary>
    /// <returns>True if there are at least 1 <see cref="Diagnostic" /> in the <see cref="DiagnosticQueue" />.</returns>
    public bool Any() => _diagnostics.Any();

    /// <summary>
    /// Gets an enumerator for the <see cref="DiagnosticQueue" /> collection (sidestepping the queue structure).
    /// </summary>
    /// <returns>The enumerator for the <see cref="DiagnosticQueue" /> as if it were a list.</returns>
    public IEnumerator GetEnumerator() => _diagnostics.GetEnumerator();

    /// <summary>
    /// Converts the <see cref="DiagnosticQueue" /> into an array (ordered from oldest -> newest item added to
    /// <see cref="DiagnosticQueue" />).
    /// </summary>
    /// <returns>Array copy of the <see cref="DiagnosticQueue" /> (not a reference).</returns>
    public Diagnostic[] ToArray() => _diagnostics.ToArray();

    /// <summary>
    /// Checks if any Diagnostics of given type.
    /// </summary>
    /// <param name="type">Type to check for, ignores all other Diagnostics.</param>
    /// <returns>If any Diagnostics of type.</returns>
    public bool Any(DiagnosticType type) {
        return _diagnostics.Where(d => d.info.severity == type).Any();
    }

    /// <summary>
    /// Pushes a <see cref="Diagnostic" /> onto the <see cref="DiagnosticQueue" />.
    /// </summary>
    /// <param name="diagnostic"><see cref="Diagnostic" /> to copy onto the <see cref="DiagnosticQueue" />.</param>
    public void Push(T diagnostic) {
        if (diagnostic != null)
            _diagnostics.Add(diagnostic);
    }

    /// <summary>
    /// Pushes a <see cref="Diagnostic" /> to the front of the <see cref="DiagnosticQueue" />.
    /// </summary>
    /// <param name="diagnostic"><see cref="Diagnostic" /> to copy onto the <see cref="DiagnosticQueue" />.</param>
    public void PushToFront(T diagnostic) {
        if (diagnostic != null)
            _diagnostics.Insert(0, diagnostic);
    }

    /// <summary>
    /// Pops all Diagnostics off <see cref="DiagnosticQueue" /> and pushes them onto this.
    /// </summary>
    /// <param name="diagnosticQueue"><see cref="DiagnosticQueue" /> to pop and copy from.</param>
    public void Move(DiagnosticQueue<T> diagnosticQueue) {
        if (diagnosticQueue == null)
            return;

        T diagnostic = diagnosticQueue.Pop();

        while (diagnostic != null) {
            _diagnostics.Add(diagnostic);
            diagnostic = diagnosticQueue.Pop();
        }
    }

    /// <summary>
    /// Pops all Diagnostics off all DiagnosticQueues and pushes them onto this.
    /// </summary>
    /// <param name="diagnosticQueues">DiagnosticQueues to pop and copy from.</param>
    public void MoveMany(IEnumerable<DiagnosticQueue<T>> diagnosticQueues) {
        if (diagnosticQueues == null)
            return;

        foreach (var diagnosticQueue in diagnosticQueues)
            Move(diagnosticQueue);
    }

    /// <summary>
    /// Removes first <see cref="Diagnostic" />.
    /// </summary>
    /// <returns>First <see cref="Diagnostic" /> on the <see cref="DiagnosticQueue" />.</returns>
    public T? Pop() {
        if (_diagnostics.Count == 0)
            return null;

        T diagnostic = _diagnostics[0];
        _diagnostics.RemoveAt(0);

        return diagnostic;
    }

    /// <summary>
    /// Removes last <see cref="Diagnostic" />.
    /// </summary>
    /// <returns>Last <see cref="Diagnostic" /> on the <see cref="DiagnosticQueue" />.</returns>
    public T? PopBack() {
        if (_diagnostics.Count == 0)
            return null;

        T diagnostic = _diagnostics[_diagnostics.Count - 1];
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
    /// Removes all Diagnostics of a specific severity (see <see cref="DiagnosticType" />).
    /// </summary>
    /// <param name="type">Severity of Diagnostics to remove.</param>
    public void Clear(DiagnosticType type) {
        for (int i=0; i<_diagnostics.Count; i++) {
            if (_diagnostics[i].info.severity == type)
                _diagnostics.RemoveAt(i--);
        }
    }

    /// <summary>
    /// Returns a list of all the Diagnostics in the <see cref="DiagnosticQueue" /> in order.
    /// </summary>
    /// <returns>List of Diagnostics (ordered oldest -> newest).</returns>
    public List<T> AsList() {
        return _diagnostics;
    }

    /// <summary>
    /// Returns a list of all the Diagnostics in the <see cref="DiagnosticQueue" /> in order, and casts them to a new
    /// <see cref="Diagnostic" /> child type.
    /// </summary>
    /// <typeparam name="NewType">Type of <see cref="Diagnostic" /> to cast existing Diagnostics to.</typeparam>
    /// <returns>List of Diagnostics (ordered oldest -> newest).</returns>
    public List<NewType> AsList<NewType>() where NewType : Diagnostic {
        return _diagnostics as List<NewType>;
    }

    /// <summary>
    /// Returns a new queue without a specific type of <see cref="Diagnostic" />, does not affect this instance.
    /// </summary>
    /// <param name="type">Which <see cref="Diagnostic" /> type to exclude.</param>
    /// <returns>New <see cref="DiagnosticQueue" /> without any Diagnostics of type <paramref name="type" />.</returns>
    public DiagnosticQueue<T> FilterOut(DiagnosticType type) {
        return new DiagnosticQueue<T>(_diagnostics.Where(d => d.info.severity != type));
    }

    /// <summary>
    /// Copies another <see cref="Diagnostic" /> queue to the front of this <see cref="DiagnosticQueue" />.
    /// </summary>
    /// <param name="queue">
    /// <see cref="DiagnosticQueue" /> to copy, does not modify this <see cref="DiagnosticQueue" />.
    /// </param>
    public void CopyToFront(DiagnosticQueue<T> queue) {
        _diagnostics.InsertRange(0, queue._diagnostics);
    }
}
