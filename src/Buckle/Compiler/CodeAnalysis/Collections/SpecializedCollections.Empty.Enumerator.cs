using System;
using System.Collections;

namespace Buckle.CodeAnalysis;

internal static partial class SpecializedCollections {
    private partial class Empty {
        internal class Enumerator : IEnumerator {
            public static readonly IEnumerator Instance = new Enumerator();

            private protected Enumerator() { }

            public object Current => throw new InvalidOperationException();

            public bool MoveNext() {
                return false;
            }

            public void Reset() {
                throw new InvalidOperationException();
            }
        }
    }
}
