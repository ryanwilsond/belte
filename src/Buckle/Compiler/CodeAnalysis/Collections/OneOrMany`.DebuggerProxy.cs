using System.Diagnostics;

namespace Buckle.CodeAnalysis;

internal readonly partial struct OneOrMany<T> {
    private sealed class DebuggerProxy(OneOrMany<T> instance) {
        private readonly OneOrMany<T> _instance = instance;

        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        internal T[] Items => _instance.ToArray();
    }
}
