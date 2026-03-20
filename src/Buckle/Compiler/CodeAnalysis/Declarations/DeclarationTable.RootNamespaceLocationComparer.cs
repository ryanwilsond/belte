using System.Collections.Generic;

namespace Buckle.CodeAnalysis;

internal sealed partial class DeclarationTable {
    private sealed class RootNamespaceLocationComparer : IComparer<SingleNamespaceDeclaration> {
        private readonly Compilation _compilation;

        internal RootNamespaceLocationComparer(Compilation compilation) {
            _compilation = compilation;
        }

        public int Compare(SingleNamespaceDeclaration x, SingleNamespaceDeclaration y) {
            return _compilation.CompareSourceLocations(x!.syntaxReference, y!.syntaxReference);
        }
    }
}
