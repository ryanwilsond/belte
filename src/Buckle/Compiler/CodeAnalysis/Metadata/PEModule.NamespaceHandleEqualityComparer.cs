using System.Collections.Generic;
using System.Reflection.Metadata;

namespace Buckle.CodeAnalysis;

internal sealed partial class PEModule {
    private class NamespaceHandleEqualityComparer : IEqualityComparer<NamespaceDefinitionHandle> {
        public static readonly NamespaceHandleEqualityComparer Singleton = new NamespaceHandleEqualityComparer();

        private NamespaceHandleEqualityComparer() { }

        public bool Equals(NamespaceDefinitionHandle x, NamespaceDefinitionHandle y) {
            return x == y;
        }

        public int GetHashCode(NamespaceDefinitionHandle obj) {
            return obj.GetHashCode();
        }
    }
}
