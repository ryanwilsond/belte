using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Buckle.CodeAnalysis;

public sealed partial class Compilation {
    internal sealed partial class ReferenceManager {
        internal sealed class MetadataReferenceEqualityComparer : IEqualityComparer<MetadataReference> {
            internal static readonly MetadataReferenceEqualityComparer Instance
                = new MetadataReferenceEqualityComparer();

            public bool Equals(MetadataReference x, MetadataReference y) {
                if (ReferenceEquals(x, y))
                    return true;

                if (x is CompilationReference cx) {
                    if (y is CompilationReference cy)
                        return (object)cx.compilation == cy.compilation;
                }

                return false;
            }

            public int GetHashCode(MetadataReference reference) {
                if (reference is CompilationReference compilationReference)
                    return RuntimeHelpers.GetHashCode(compilationReference.compilation);

                return RuntimeHelpers.GetHashCode(reference);
            }
        }
    }
}
