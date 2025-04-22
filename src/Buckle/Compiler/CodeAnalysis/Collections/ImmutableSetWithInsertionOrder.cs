using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Buckle.CodeAnalysis;

internal sealed class ImmutableSetWithInsertionOrder<T> : IEnumerable<T> where T : notnull {
    internal static readonly ImmutableSetWithInsertionOrder<T> Empty =
        new ImmutableSetWithInsertionOrder<T>(ImmutableDictionary.Create<T, uint>(), 0u);

    private readonly ImmutableDictionary<T, uint> _map;
    private readonly uint _nextElementValue;

    private ImmutableSetWithInsertionOrder(ImmutableDictionary<T, uint> map, uint nextElementValue) {
        _map = map;
        _nextElementValue = nextElementValue;
    }

    public int Count => _map.Count;

    internal bool Contains(T value) {
        return _map.ContainsKey(value);
    }

    internal ImmutableSetWithInsertionOrder<T> Add(T value) {
        if (_map.ContainsKey(value))
            return this;

        return new ImmutableSetWithInsertionOrder<T>(_map.Add(value, _nextElementValue), _nextElementValue + 1u);
    }

    internal ImmutableSetWithInsertionOrder<T> Remove(T value) {
        var modifiedMap = _map.Remove(value);

        if (modifiedMap == _map)
            return this;

        return Count == 1 ? Empty : new ImmutableSetWithInsertionOrder<T>(modifiedMap, _nextElementValue);
    }

    internal IEnumerable<T> InInsertionOrder => _map.OrderBy(kv => kv.Value).Select(kv => kv.Key);

    public override string ToString() {
        return "{" + string.Join(", ", this) + "}";
    }

    public IEnumerator<T> GetEnumerator() {
        return _map.Keys.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() {
        return _map.Keys.GetEnumerator();
    }
}
