using System.Collections.Generic;

namespace Buckle.CodeAnalysis;

internal class KeyedStack<T, R> where T : notnull {
    private readonly Dictionary<T, Stack<R>> _dict = [];

    internal void Push(T key, R value) {
        if (!_dict.TryGetValue(key, out var store)) {
            store = new Stack<R>();
            _dict.Add(key, store);
        }

        store.Push(value);
    }

    internal bool TryPop(T key, out R value) {
        if (_dict.TryGetValue(key, out var store) && store.Count > 0) {
            value = store.Pop();
            return true;
        }

        value = default;
        return false;
    }
}
