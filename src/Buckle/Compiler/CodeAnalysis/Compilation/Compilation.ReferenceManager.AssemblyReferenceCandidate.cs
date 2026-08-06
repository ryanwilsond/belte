using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis;

public sealed partial class Compilation {
    internal sealed partial class ReferenceManager {
        private readonly struct AssemblyReferenceCandidate {
            internal readonly int definitionIndex;
            internal readonly AssemblySymbol assemblySymbol;

            internal AssemblyReferenceCandidate(int definitionIndex, AssemblySymbol symbol) {
                this.definitionIndex = definitionIndex;
                assemblySymbol = symbol;
            }
        }
    }
}
