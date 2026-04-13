using System;
using System.Collections.Immutable;
using System.Linq;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Diagnostics;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis;

public sealed partial class Compilation {
    internal sealed class ReferenceManager {
        private readonly ImmutableArray<AssemblyMetadata> _assemblies;
        private readonly ImmutableArray<PEAssemblySymbol> _assemblySymbols;
        private readonly ImmutableArray<AssemblySymbol> _referencedAssemblies;
        private readonly ImmutableArray<UnifiedAssembly<AssemblySymbol>> _unifiedAssemblies;
        private readonly ImmutableArray<ModuleReferences<AssemblySymbol>> _referencedModulesReferences;

        internal ReferenceManager(string[] references, BelteDiagnosticQueue diagnostics) {
            var builder = ArrayBuilder<AssemblyMetadata>.GetInstance();

            foreach (var reference in references) {
                var assembly = AssemblyMetadata.CreateFromFile(reference);

                try {
                    if (!assembly.IsValidAssembly())
                        diagnostics.Push(Error.InvalidReference(reference));
                    else
                        builder.Add(assembly);
                } catch (BadImageFormatException) {
                    diagnostics.Push(Error.InvalidReference(reference));
                }
            }

            _assemblies = builder.ToImmutableAndFree();
            _referencedAssemblies = [];
            _unifiedAssemblies = [];
            _referencedModulesReferences = [];

            var assemblyBuilder = ArrayBuilder<PEAssemblySymbol>.GetInstance();

            foreach (var assembly in _assemblies) {
                var assemblySymbol = CreatePEAssemblyForAssemblyMetadataFirstPass(assembly, MetadataImportOptions.All);
                assemblyBuilder.Add(assemblySymbol);
            }

            _assemblySymbols = assemblyBuilder.ToImmutableAndFree();
            _referencedAssemblies = _assemblySymbols.CastArray<AssemblySymbol>();

            foreach (var assemblySymbol in _assemblySymbols)
                CreatePEAssemblyForAssemblyMetadataSecondPass(assemblySymbol, assemblySymbol.assembly, out _);
        }

        internal NamespaceSymbol[] GetGlobalNamespaces() {
            return _assemblySymbols.Select(a => a.globalNamespace).ToArray();
        }

        internal PEAssemblySymbol CreatePEAssemblyForAssemblyMetadataFirstPass(
            AssemblyMetadata metadata,
            MetadataImportOptions importOptions) {
            var assembly = metadata.GetAssembly();

            var assemblySymbol = new PEAssemblySymbol(assembly, isLinked: true, importOptions: importOptions);
            return assemblySymbol;
        }

        internal void CreatePEAssemblyForAssemblyMetadataSecondPass(
            AssemblySymbol assemblySymbol,
            PEAssembly assembly,
            out ImmutableDictionary<AssemblyIdentity, AssemblyIdentity> assemblyReferenceIdentityMap) {
            var referencedAssembliesByIdentity = new AssemblyIdentityMap<AssemblySymbol>();

            foreach (var symbol in _referencedAssemblies)
                referencedAssembliesByIdentity.Add(symbol.identity, symbol);

            var peReferences = assembly.assemblyReferences.SelectAsArray(
                MapAssemblyIdentityToResolvedSymbol,
                referencedAssembliesByIdentity
            );

            assemblyReferenceIdentityMap = GetAssemblyReferenceIdentityBaselineMap(
                peReferences,
                assembly.assemblyReferences
            );

            var unifiedAssemblies = _unifiedAssemblies.WhereAsArray(
                (unified, referencedAssembliesByIdentity)
                    => referencedAssembliesByIdentity.Contains(
                        unified.originalReference,
                        allowHigherVersion: false),
                    referencedAssembliesByIdentity);

            InitializeAssemblyReuseData(assemblySymbol, peReferences, unifiedAssemblies);

            if (assembly.ContainsNoPiaLocalTypes())
                assemblySymbol.SetNoPiaResolutionAssemblies(_referencedAssemblies);
        }

        private static AssemblySymbol MapAssemblyIdentityToResolvedSymbol(
            AssemblyIdentity identity,
            AssemblyIdentityMap<AssemblySymbol> map) {
            if (map.TryGetValue(identity, out var symbol))
                return symbol;

            return new MissingAssemblySymbol(identity);
        }

        private void InitializeAssemblyReuseData(
            AssemblySymbol assemblySymbol,
            ImmutableArray<AssemblySymbol> referencedAssemblies,
            ImmutableArray<UnifiedAssembly<AssemblySymbol>> unifiedAssemblies) {
            var sourceModuleReferences = new ModuleReferences<AssemblySymbol>(
                referencedAssemblies.SelectAsArray(a => a.identity),
                referencedAssemblies,
                unifiedAssemblies
            );

            assemblySymbol.modules[0].SetReferences(sourceModuleReferences);

            var assemblyModules = assemblySymbol.modules;
            var referencedModulesReferences = _referencedModulesReferences;

            for (var i = 1; i < assemblyModules.Length; i++)
                assemblyModules[i].SetReferences(referencedModulesReferences[i - 1]);
        }

        internal static ImmutableDictionary<AssemblyIdentity, AssemblyIdentity> GetAssemblyReferenceIdentityBaselineMap(
            ImmutableArray<AssemblySymbol> symbols,
            ImmutableArray<AssemblyIdentity> originalIdentities) {
            ImmutableDictionary<AssemblyIdentity, AssemblyIdentity>.Builder? lazyBuilder = null;

            for (var i = 0; i < originalIdentities.Length; i++) {
                var symbolIdentity = symbols[i].identity;
                var originalIdentity = originalIdentities[i];
                // TODO Versioning
            }

            return lazyBuilder?.ToImmutable() ?? [];
        }
    }
}
