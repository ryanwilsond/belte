using System.Diagnostics;
using Buckle.CodeAnalysis.Symbols;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis;

public sealed partial class Compilation {
    internal sealed partial class ReferenceManager {
        private sealed class AssemblyDataForFile : AssemblyDataForMetadataOrCompilation {
            internal readonly PEAssembly assembly;
            internal readonly WeakList<AssemblySymbol> cachedSymbols;

            private readonly MetadataImportOptions _compilationImportOptions;
            private readonly string _sourceAssemblySimpleName;

            private bool _internalsVisibleComputed;
            private bool _internalsPotentiallyVisibleToCompilation;

            internal AssemblyDataForFile(
                PEAssembly assembly,
                WeakList<AssemblySymbol> cachedSymbols,
                bool embedInteropTypes,
                string sourceAssemblySimpleName,
                MetadataImportOptions compilationImportOptions)
                : base(assembly.identity, assembly.assemblyReferences, embedInteropTypes) {
                Debug.Assert(cachedSymbols is not null);

                this.cachedSymbols = cachedSymbols;
                this.assembly = assembly;
                _compilationImportOptions = compilationImportOptions;
                _sourceAssemblySimpleName = sourceAssemblySimpleName;
            }

            internal bool internalsMayBeVisibleToCompilation {
                get {
                    if (!_internalsVisibleComputed) {
                        // TODO internals visible to
                        _internalsPotentiallyVisibleToCompilation = false;
                        // InternalsMayBeVisibleToAssemblyBeingCompiled(_sourceAssemblySimpleName, Assembly);
                        _internalsVisibleComputed = true;
                    }

                    return _internalsPotentiallyVisibleToCompilation;
                }
            }

            internal MetadataImportOptions effectiveImportOptions {
                get {
                    if (internalsMayBeVisibleToCompilation &&
                        _compilationImportOptions == MetadataImportOptions.Internal) {
                        return MetadataImportOptions.Internal;
                    }

                    return _compilationImportOptions;
                }
            }

            internal override bool containsNoPiaLocalTypes => assembly.ContainsNoPiaLocalTypes();

            internal override bool declaresTheObjectClass => assembly.declaresTheObjectClass;

            internal override Compilation sourceCompilation => null;

            internal override AssemblySymbol CreateAssemblySymbol() {
                return new PEAssemblySymbol(assembly, isLinked, effectiveImportOptions);
            }

            private protected override void AddAvailableSymbols(ArrayBuilder<AssemblySymbol> assemblies) {
                lock (SymbolCacheAndReferenceManagerStateGuard) {
                    foreach (var assembly in cachedSymbols) {
                        var peAssembly = assembly as PEAssemblySymbol;

                        if (IsMatchingAssembly(peAssembly))
                            assemblies.Add(peAssembly);
                    }
                }
            }

            internal override bool IsMatchingAssembly(AssemblySymbol candidateAssembly) {
                return IsMatchingAssembly(candidateAssembly as PEAssemblySymbol);
            }

            private bool IsMatchingAssembly(PEAssemblySymbol peAssembly) {
                if (peAssembly is null)
                    return false;

                if (!ReferenceEquals(peAssembly.assembly, assembly))
                    return false;

                if (effectiveImportOptions != peAssembly.primaryModule.importOptions)
                    return false;

                return true;
            }
        }
    }
}
