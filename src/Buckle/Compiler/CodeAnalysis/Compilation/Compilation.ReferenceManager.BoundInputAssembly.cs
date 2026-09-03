using System.Diagnostics;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis;

public sealed partial class Compilation {
    internal sealed partial class ReferenceManager {
        [DebuggerDisplay("{GetDebuggerDisplay(), nq}")]
        internal struct BoundInputAssembly {
            internal AssemblySymbol assemblySymbol;
            internal AssemblyReferenceBinding[] referenceBinding;

            private string GetDebuggerDisplay() {
                return assemblySymbol is null ? "?" : assemblySymbol.ToString();
            }
        }
    }
}
