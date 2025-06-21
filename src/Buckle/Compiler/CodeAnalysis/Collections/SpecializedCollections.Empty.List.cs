using System;
using System.Collections.Generic;

namespace Buckle.CodeAnalysis;

internal static partial class SpecializedCollections {
    private partial class Empty {
        internal class List<T> : Collection<T>, IList<T>, IReadOnlyList<T> {
            public static new readonly List<T> Instance = new();

            private protected List() { }

            public int IndexOf(T item) {
                return -1;
            }

            public void Insert(int index, T item) {
                throw new NotSupportedException();
            }

            public void RemoveAt(int index) {
                throw new NotSupportedException();
            }

            public T this[int index] {
                get {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }

                set {
                    throw new NotSupportedException();
                }
            }
        }
    }
}
