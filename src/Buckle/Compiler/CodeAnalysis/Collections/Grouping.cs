using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Buckle.CodeAnalysis;

internal class Grouping<TKey, TElement> : IGrouping<TKey, TElement> where TKey : notnull {
    private readonly IEnumerable<TElement> _elements;

    internal Grouping(TKey key, IEnumerable<TElement> elements) {
        Key = key;
        _elements = elements;
    }

    internal Grouping(KeyValuePair<TKey, IEnumerable<TElement>> pair)
        : this(pair.Key, pair.Value) {
    }

    public TKey Key { get; }

    public IEnumerator<TElement> GetEnumerator() {
        return _elements.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() {
        return GetEnumerator();
    }
}
