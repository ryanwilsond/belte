
namespace Buckle.CodeAnalysis;

public sealed partial class Compilation {
    internal sealed partial class ReferenceManager {
        private readonly struct ReferencedAssemblyIdentity {
            internal readonly AssemblyIdentity identity;
            internal readonly MetadataReference reference;

            internal readonly int relativeAssemblyIndex;

            internal ReferencedAssemblyIdentity(
                AssemblyIdentity identity,
                MetadataReference reference,
                int relativeAssemblyIndex) {
                this.identity = identity;
                this.reference = reference;
                this.relativeAssemblyIndex = relativeAssemblyIndex;
            }

            internal int GetAssemblyIndex(int explicitlyReferencedAssemblyCount) {
                return relativeAssemblyIndex >= 0
                    ? relativeAssemblyIndex
                    : explicitlyReferencedAssemblyCount + relativeAssemblyIndex;
            }
        }
    }
}
