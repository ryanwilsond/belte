using System.Collections.Immutable;
using System.Diagnostics;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis;

public sealed partial class Compilation {
    internal sealed partial class ReferenceManager {
        [DebuggerDisplay("{GetDebuggerDisplay(), nq}")]
        internal abstract class AssemblyData {
            internal abstract AssemblyIdentity identity { get; }

            internal abstract ImmutableArray<AssemblyIdentity> assemblyReferences { get; }

            internal abstract ImmutableArray<AssemblySymbol> availableSymbols { get; }

            internal abstract bool containsNoPiaLocalTypes { get; }

            internal abstract bool isLinked { get; }

            internal abstract bool declaresTheObjectClass { get; }

            internal abstract Compilation sourceCompilation { get; }

            internal abstract bool IsMatchingAssembly(AssemblySymbol assembly);

            internal abstract AssemblyReferenceBinding[] BindAssemblyReferences(
                MultiDictionary<string, (AssemblyData DefinitionData, int DefinitionIndex)> assemblies,
                AssemblyIdentityComparer assemblyIdentityComparer);

            private string GetDebuggerDisplay() {
                return $"{GetType().Name}: [{identity.GetDisplayName()}]";
            }
        }
    }
}
