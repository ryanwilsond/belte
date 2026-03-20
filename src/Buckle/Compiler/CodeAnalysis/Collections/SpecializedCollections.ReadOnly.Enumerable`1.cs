using System.Collections;

namespace Buckle.CodeAnalysis;

internal partial class SpecializedCollections {
    private partial class ReadOnly {
        internal class Enumerable<TUnderlying> : IEnumerable
            where TUnderlying : IEnumerable {
            private protected readonly TUnderlying underlying;

            internal Enumerable(TUnderlying underlying) {
                this.underlying = underlying;
            }

            public IEnumerator GetEnumerator() {
                return underlying.GetEnumerator();
            }
        }
    }
}
