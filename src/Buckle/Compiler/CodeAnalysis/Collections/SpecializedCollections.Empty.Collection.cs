using System;
using System.Collections.Generic;

namespace Buckle.CodeAnalysis;

internal static partial class SpecializedCollections {
    private partial class Empty {
        internal class Collection<T> : Enumerable<T>, ICollection<T> {
            public static readonly ICollection<T> Instance = [];

            private protected Collection() { }

            public void Add(T item) {
                throw new NotSupportedException();
            }

            public void Clear() {
                throw new NotSupportedException();
            }

            public bool Contains(T item) {
                return false;
            }

            public void CopyTo(T[] array, int arrayIndex) { }

            public int Count => 0;

            public bool IsReadOnly => true;

            public bool Remove(T item) {
                throw new NotSupportedException();
            }
        }
    }
}
