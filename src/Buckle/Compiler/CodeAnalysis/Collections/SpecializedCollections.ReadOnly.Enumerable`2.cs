using System.Collections.Generic;

namespace Buckle.CodeAnalysis;

internal partial class SpecializedCollections {
    private partial class ReadOnly {
        internal class Enumerable<TUnderlying, T> : Enumerable<TUnderlying>, IEnumerable<T>
            where TUnderlying : IEnumerable<T> {
            internal Enumerable(TUnderlying underlying) : base(underlying) { }

            public new IEnumerator<T> GetEnumerator() {
                return underlying.GetEnumerator();
            }
        }
    }
}
