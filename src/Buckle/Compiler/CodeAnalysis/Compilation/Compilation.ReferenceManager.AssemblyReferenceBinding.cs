using System.Diagnostics;

namespace Buckle.CodeAnalysis;

public sealed partial class Compilation {
    internal sealed partial class ReferenceManager {
        [DebuggerDisplay("{GetDebuggerDisplay(), nq}")]
        internal readonly struct AssemblyReferenceBinding {
            private readonly int _definitionIndex;
            private readonly int _versionDifference;

            internal AssemblyReferenceBinding(AssemblyIdentity referenceIdentity) {
                Debug.Assert(referenceIdentity is not null);

                this.referenceIdentity = referenceIdentity;
                _definitionIndex = -1;
                _versionDifference = 0;
            }

            internal AssemblyReferenceBinding(
                AssemblyIdentity referenceIdentity,
                int definitionIndex,
                int versionDifference = 0) {
                Debug.Assert(referenceIdentity is not null);
                Debug.Assert(definitionIndex >= 0);
                Debug.Assert(versionDifference >= -1 && versionDifference <= +1);

                this.referenceIdentity = referenceIdentity;
                _definitionIndex = definitionIndex;
                _versionDifference = versionDifference;
            }

            internal bool boundToAssemblyBeingBuilt => _definitionIndex == 0;

            internal bool isBound => _definitionIndex >= 0;

            internal int versionDifference {
                get {
                    Debug.Assert(isBound);
                    return _versionDifference;
                }
            }

            internal int definitionIndex {
                get {
                    Debug.Assert(isBound);
                    return _definitionIndex;
                }
            }

            internal AssemblyIdentity referenceIdentity { get; }

            private string GetDebuggerDisplay() {
                var displayName = referenceIdentity?.GetDisplayName() ?? "";
                return isBound ? displayName + " -> #" + definitionIndex + (versionDifference != 0 ? " VersionDiff=" + versionDifference : "") : "unbound";
            }
        }
    }
}
