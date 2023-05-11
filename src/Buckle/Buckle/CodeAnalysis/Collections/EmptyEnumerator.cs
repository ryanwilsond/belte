using System.Collections;
using System.Collections.Generic;

public class EmptyEnumerator<T> : IEnumerator<T> {
    public T Current => default(T);

    object IEnumerator.Current => this.Current;

    public void Dispose() { }

    public bool MoveNext() {
        return false;
    }

    public void Reset() { }
}
