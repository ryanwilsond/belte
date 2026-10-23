using System.Collections.Immutable;
using System.Diagnostics;
using Buckle.CodeAnalysis.Symbols;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis;

public sealed partial class Compilation {
    internal sealed partial class ReferenceManager {
        private abstract class AssemblyDataForMetadataOrCompilation : AssemblyData {
            private ImmutableArray<AssemblySymbol> _assemblies;
            private readonly AssemblyIdentity _identity;
            private readonly ImmutableArray<AssemblyIdentity> _referencedAssemblies;
            private readonly bool _embedInteropTypes;

            protected AssemblyDataForMetadataOrCompilation(
                AssemblyIdentity identity,
                ImmutableArray<AssemblyIdentity> referencedAssemblies,
                bool embedInteropTypes) {
                Debug.Assert(identity is not null);
                Debug.Assert(!referencedAssemblies.IsDefault);

                _embedInteropTypes = embedInteropTypes;
                _identity = identity;
                _referencedAssemblies = referencedAssemblies;
            }

            internal override AssemblyIdentity identity => _identity;

            internal override ImmutableArray<AssemblySymbol> availableSymbols {
                get {
                    if (_assemblies.IsDefault) {
                        var builder = ArrayBuilder<AssemblySymbol>.GetInstance();
                        AddAvailableSymbols(builder);
                        _assemblies = builder.ToImmutableAndFree();
                    }

                    return _assemblies;
                }
            }

            internal override ImmutableArray<AssemblyIdentity> assemblyReferences => _referencedAssemblies;

            internal sealed override bool isLinked => _embedInteropTypes;

            internal abstract AssemblySymbol CreateAssemblySymbol();

            private protected abstract void AddAvailableSymbols(ArrayBuilder<AssemblySymbol> builder);

            internal override AssemblyReferenceBinding[] BindAssemblyReferences(
                MultiDictionary<string, (AssemblyData DefinitionData, int DefinitionIndex)> assemblies,
                AssemblyIdentityComparer assemblyIdentityComparer) {
                return ResolveReferencedAssemblies(
                    _referencedAssemblies,
                    assemblies,
                    resolveAgainstAssemblyBeingBuilt: true,
                    assemblyIdentityComparer: assemblyIdentityComparer
                );
            }
        }
    }
}
