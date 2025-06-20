using System.Collections.Generic;

namespace Buckle.CodeAnalysis;

internal static partial class SpecializedCollections {
    private partial class Empty {
        internal class Enumerable<T> : IEnumerable<T> {
            private readonly IEnumerator<T> _enumerator = Enumerator<T>.Instance;

            public IEnumerator<T> GetEnumerator() {
                return _enumerator;
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {
                return GetEnumerator();
            }
        }
    }
}
