using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Buckle.CodeAnalysis;

internal sealed partial class MultiDictionary<K, V> where K : notnull {
    internal readonly partial struct ValueSet {
        internal struct Enumerator : IEnumerator<V> {
            private readonly V _value;
            private ImmutableHashSet<V>.Enumerator _values;
            private int _count;

            internal Enumerator(ValueSet v) {
                if (v._value is null) {
                    _value = default;
                    _values = default;
                    _count = 0;
                } else {
                    if (v._value is not ImmutableHashSet<V> set) {
                        _value = (V)v._value;
                        _values = default;
                        _count = 1;
                    } else {
                        _value = default;
                        _values = set.GetEnumerator();
                        _count = set.Count;
                    }
                }
            }

            public V Current {
                get {
                    return _count > 1 ? _values.Current : _value;
                }
            }

            public void Dispose() { }

            public void Reset() {
                throw new NotSupportedException();
            }

            object IEnumerator.Current => Current;

            public bool MoveNext() {
                switch (_count) {
                    case 0:
                        return false;
                    case 1:
                        _count = 0;
                        return true;
                    default:
                        if (_values.MoveNext())
                            return true;

                        _count = 0;
                        return false;
                }
            }
        }
    }
}
