using System;
using System.Collections.Generic;

namespace Buckle.CodeAnalysis;

internal partial class SpecializedCollections {
    private static partial class ReadOnly {
        internal class Collection<TUnderlying, T> : Enumerable<TUnderlying, T>, ICollection<T>
            where TUnderlying : ICollection<T> {
            internal Collection(TUnderlying underlying) : base(underlying) { }

            public void Add(T item) {
                throw new NotSupportedException();
            }

            public void Clear() {
                throw new NotSupportedException();
            }

            public bool Contains(T item) {
                return underlying.Contains(item);
            }

            public void CopyTo(T[] array, int arrayIndex) {
                underlying.CopyTo(array, arrayIndex);
            }

            public int Count => underlying.Count;

            public bool IsReadOnly => true;

            public bool Remove(T item) {
                throw new NotSupportedException();
            }
        }
    }
}
