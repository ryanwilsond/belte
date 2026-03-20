using System;
using System.Collections.Generic;

namespace Buckle.CodeAnalysis;

internal static partial class SpecializedCollections {
    private partial class Empty {
        internal class Enumerator<T> : Enumerator, IEnumerator<T> {
            public static new readonly IEnumerator<T> Instance = new Enumerator<T>();

            private protected Enumerator() { }

            public new T Current => throw new InvalidOperationException();

            public void Dispose() { }
        }
    }
}
