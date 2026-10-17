using System.Collections.Immutable;
using System.Diagnostics;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis;

public sealed partial class Compilation {
    internal sealed partial class ReferenceManager {
        private sealed class AssemblyDataForAssemblyBeingBuilt : AssemblyData {
            private readonly AssemblyIdentity _assemblyIdentity;
            private readonly ImmutableArray<AssemblyData> _referencedAssemblyData;
            private readonly ImmutableArray<AssemblyIdentity> _referencedAssemblies;

            internal AssemblyDataForAssemblyBeingBuilt(
                AssemblyIdentity identity,
                ImmutableArray<AssemblyData> referencedAssemblyData,
                ImmutableArray<PEModule> modules) {
                Debug.Assert(identity is not null);
                Debug.Assert(!referencedAssemblyData.IsDefault);

                _assemblyIdentity = identity;

                _referencedAssemblyData = referencedAssemblyData;

                var builderSize = referencedAssemblyData.Length +
                    modules.Sum(static module => module.referencedAssemblies.Length);

                var refs = ArrayBuilder<AssemblyIdentity>.GetInstance(builderSize);

                foreach (var data in referencedAssemblyData)
                    refs.Add(data.identity);

                for (var i = 0; i < modules.Length; i++)
                    refs.AddRange(modules[i].referencedAssemblies);

                _referencedAssemblies = refs.ToImmutableAndFree();
            }

            internal override AssemblyIdentity identity => _assemblyIdentity;

            internal override ImmutableArray<AssemblyIdentity> assemblyReferences => _referencedAssemblies;

            internal override ImmutableArray<AssemblySymbol> availableSymbols => throw ExceptionUtilities.Unreachable();

            internal override bool containsNoPiaLocalTypes => throw ExceptionUtilities.Unreachable();

            internal override bool isLinked => false;

            internal override bool declaresTheObjectClass => false;

            internal override Compilation sourceCompilation => null;

            internal override AssemblyReferenceBinding[] BindAssemblyReferences(
                MultiDictionary<string, (AssemblyData DefinitionData, int DefinitionIndex)> assemblies,
                AssemblyIdentityComparer assemblyIdentityComparer) {
                var boundReferences = new AssemblyReferenceBinding[_referencedAssemblies.Length];

                for (var i = 0; i < _referencedAssemblyData.Length; i++) {
                    Debug.Assert(assemblies[_referencedAssemblyData[i].identity.name]
                        .Contains((_referencedAssemblyData[i], i + 1)));

                    boundReferences[i] = new AssemblyReferenceBinding(_referencedAssemblyData[i].identity, i + 1);
                }

                for (var i = _referencedAssemblyData.Length; i < _referencedAssemblies.Length; i++) {
                    boundReferences[i] = ResolveReferencedAssembly(
                        _referencedAssemblies[i],
                        assemblies,
                        resolveAgainstAssemblyBeingBuilt: false,
                        assemblyIdentityComparer
                    );
                }

                return boundReferences;
            }

            internal override bool IsMatchingAssembly(AssemblySymbol assembly) {
                throw ExceptionUtilities.Unreachable();
            }
        }
    }
}
