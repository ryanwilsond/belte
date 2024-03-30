using System.Collections;
using System.Collections.Generic;

namespace Buckle.CodeAnalysis;

/// <summary>
/// An empty enumerator that cannot move.
/// </summary>
public class EmptyEnumerator<T> : IEnumerator<T> {
    public T Current => default;

    object IEnumerator.Current => Current;

    public void Dispose() { }

    public bool MoveNext() {
        return false;
    }

    public void Reset() { }
}
