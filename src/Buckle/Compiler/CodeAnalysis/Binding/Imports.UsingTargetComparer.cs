using System.Collections.Generic;

namespace Buckle.CodeAnalysis.Binding;

internal sealed partial class Imports {
    private class UsingTargetComparer : IEqualityComparer<NamespaceOrTypeAndUsingDirective> {
        internal static readonly IEqualityComparer<NamespaceOrTypeAndUsingDirective> Instance
            = new UsingTargetComparer();

        private UsingTargetComparer() { }

        bool IEqualityComparer<NamespaceOrTypeAndUsingDirective>.Equals(
            NamespaceOrTypeAndUsingDirective x,
            NamespaceOrTypeAndUsingDirective y) {
            return x.namespaceOrType.Equals(y.namespaceOrType);
        }

        int IEqualityComparer<NamespaceOrTypeAndUsingDirective>.GetHashCode(NamespaceOrTypeAndUsingDirective obj) {
            return obj.namespaceOrType.GetHashCode();
        }
    }
}
