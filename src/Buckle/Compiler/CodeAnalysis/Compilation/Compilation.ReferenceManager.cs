using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
using Buckle.Libraries;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis;

public sealed partial class Compilation {
    internal sealed partial class ReferenceManager {
        internal static object SymbolCacheAndReferenceManagerStateGuard = new object();

        private static readonly ImmutableArray<string> SupersededAlias = ImmutableArray.Create("<superseded>");

        private static readonly ObjectPool<MultiDictionary<string, (AssemblyData DefinitionData, int DefinitionIndex)>> Pool =
            new ObjectPool<MultiDictionary<string, (AssemblyData DefinitionData, int DefinitionIndex)>>(
                () => new MultiDictionary<string, (AssemblyData DefinitionData, int DefinitionIndex)>(
                    AssemblyIdentityComparer.SimpleNameComparer));

        private static readonly ObjectPool<Queue<AssemblyReferenceCandidate>> CandidatesToExaminePool
            = new ObjectPool<Queue<AssemblyReferenceCandidate>>(() => new Queue<AssemblyReferenceCandidate>());

        private static readonly ObjectPool<List<AssemblySymbol>> CandidateReferencedSymbolsPool
            = new ObjectPool<List<AssemblySymbol>>(() => new List<AssemblySymbol>(capacity: 1024));

        internal readonly Dictionary<MetadataReference, object> observedMetadata;

        private int _isBound;
        private ThreeState _lazyHasCircularReference;
        private ImmutableArray<PEModule> _lazyReferencedModules;
        private ImmutableArray<AssemblySymbol> _lazyReferencedAssemblies;
        private ImmutableArray<UnifiedAssembly<AssemblySymbol>> _lazyUnifiedAssemblies;
        private ImmutableArray<ModuleReferences<AssemblySymbol>> _lazyReferencedModulesReferences;
        private Dictionary<MetadataReference, int> _lazyReferencedAssembliesMap;
        private Dictionary<MetadataReference, int> _lazyReferencedModuleIndexMap;
        private BelteDiagnosticQueue _lazyDiagnostics;
        private IDictionary<(string, string), MetadataReference> _lazyReferenceDirectiveMap;
        private ImmutableArray<MetadataReference> _lazyDirectiveReferences;
        private ImmutableArray<MetadataReference> _lazyExplicitReferences;
        private ImmutableArray<ImmutableArray<string>> _lazyAliasesOfReferencedAssemblies;
        private ImmutableDictionary<MetadataReference, ImmutableArray<MetadataReference>> _lazyMergedAssemblyReferencesMap;
        private AssemblySymbol _lazyCorAssemblyOpt;

        internal ReferenceManager(
            CorLibrary corLibrary,
            string simpleAssemblyName,
            AssemblyIdentityComparer identityComparer,
            Dictionary<MetadataReference, object> observedMetadata) {
            this.simpleAssemblyName = simpleAssemblyName;
            this.identityComparer = identityComparer;
            this.observedMetadata = observedMetadata ?? [];
            this.corLibrary = corLibrary;
        }

        internal string simpleAssemblyName { get; }

        internal CorLibrary corLibrary { get; }

        internal AssemblyIdentityComparer identityComparer { get; }

        internal bool isBound => _isBound != 0;

        internal bool hasCircularReference {
            get {
                AssertBound();
                return _lazyHasCircularReference == ThreeState.True;
            }
        }

        internal ImmutableArray<PEModule> referencedModules {
            get {
                AssertBound();
                return _lazyReferencedModules;
            }
        }

        internal ImmutableArray<AssemblySymbol> referencedAssemblies {
            get {
                AssertBound();
                return _lazyReferencedAssemblies;
            }
        }

        internal ImmutableArray<UnifiedAssembly<AssemblySymbol>> unifiedAssemblies {
            get {
                AssertBound();
                return _lazyUnifiedAssemblies;
            }
        }

        internal ImmutableArray<ModuleReferences<AssemblySymbol>> referencedModulesReferences {
            get {
                AssertBound();
                return _lazyReferencedModulesReferences;
            }
        }

        internal BelteDiagnosticQueue diagnostics {
            get {
                AssertBound();
                return _lazyDiagnostics;
            }
        }

        internal ImmutableArray<ImmutableArray<string>> aliasesOfReferencedAssemblies {
            get {
                AssertBound();
                return _lazyAliasesOfReferencedAssemblies;
            }
        }

        internal ImmutableArray<MetadataReference> explicitReferences {
            get {
                AssertBound();
                return _lazyExplicitReferences;
            }
        }

        internal AssemblySymbol corAssemblyOpt {
            get {
                AssertBound();
                return _lazyCorAssemblyOpt;
            }
        }

        [Conditional("DEBUG")]
        internal void AssertBound() {
            Debug.Assert(_isBound != 0);
            Debug.Assert(_lazyHasCircularReference != ThreeState.Unknown);
            Debug.Assert(_lazyReferencedAssembliesMap is not null);
            Debug.Assert(_lazyReferencedModuleIndexMap is not null);
            Debug.Assert(_lazyReferenceDirectiveMap is not null);
            Debug.Assert(!_lazyDirectiveReferences.IsDefault);
            // Debug.Assert(_lazyImplicitReferenceResolutions is not null);
            Debug.Assert(!_lazyExplicitReferences.IsDefault);
            Debug.Assert(!_lazyReferencedModules.IsDefault);
            Debug.Assert(!_lazyReferencedModulesReferences.IsDefault);
            Debug.Assert(!_lazyReferencedAssemblies.IsDefault);
            Debug.Assert(!_lazyAliasesOfReferencedAssemblies.IsDefault);
            Debug.Assert(_lazyMergedAssemblyReferencesMap is not null);
            Debug.Assert(!_lazyUnifiedAssemblies.IsDefault);
        }

        [Conditional("DEBUG")]
        internal void AssertUnbound() {
            Debug.Assert(_isBound == 0);
            Debug.Assert(_lazyHasCircularReference == ThreeState.Unknown);
            Debug.Assert(_lazyReferencedAssembliesMap is null);
            Debug.Assert(_lazyReferencedModuleIndexMap is null);
            Debug.Assert(_lazyReferenceDirectiveMap is null);
            Debug.Assert(_lazyDirectiveReferences.IsDefault);
            // Debug.Assert(_lazyImplicitReferenceResolutions is null);
            Debug.Assert(_lazyExplicitReferences.IsDefault);
            Debug.Assert(_lazyReferencedModules.IsDefault);
            Debug.Assert(_lazyReferencedModulesReferences.IsDefault);
            Debug.Assert(_lazyReferencedAssemblies.IsDefault);
            Debug.Assert(_lazyAliasesOfReferencedAssemblies.IsDefault);
            Debug.Assert(_lazyMergedAssemblyReferencesMap is null);
            Debug.Assert(_lazyUnifiedAssemblies.IsDefault);
            // Debug.Assert(_lazyCorLibraryOpt is null);
        }

        [Conditional("DEBUG")]
        internal void AssertCanReuseForCompilation(Compilation compilation) {
            Debug.Assert(compilation.assemblyName == simpleAssemblyName);
        }

        internal bool DeclarationsAccessibleWithoutAlias(int referencedAssemblyIndex) {
            var aliases = aliasesOfReferencedAssemblies[referencedAssemblyIndex];
            return aliases.Length == 0 ||
                aliases.IndexOf(MetadataReferenceProperties.GlobalAlias, StringComparer.Ordinal) >= 0;
        }

        internal void CreateSourceAssemblyForCompilation(Compilation compilation) {
            if (!isBound && CreateAndSetSourceAssemblyFullBind(compilation)) {
            } else if (!hasCircularReference) {
                CreateAndSetSourceAssemblyReuseData(compilation);
            } else {
                var newManager = new ReferenceManager(
                    corLibrary,
                    simpleAssemblyName,
                    identityComparer,
                    observedMetadata
                );

                var successful = newManager.CreateAndSetSourceAssemblyFullBind(compilation);

                Debug.Assert(successful);

                newManager.AssertBound();
            }

            AssertBound();
            Debug.Assert(compilation._lazyAssembly is not null);
        }

        private void CreateAndSetSourceAssemblyReuseData(Compilation compilation) {
            AssertBound();

            Debug.Assert(!hasCircularReference);

            var moduleName = compilation.MakeSourceModuleName();
            var assemblySymbol = new SourceAssemblySymbol(
                compilation,
                simpleAssemblyName,
                moduleName,
                referencedModules
            );

            InitializeAssemblyReuseData(assemblySymbol, referencedAssemblies, unifiedAssemblies);

            if (compilation._lazyAssembly is null) {
                lock (SymbolCacheAndReferenceManagerStateGuard) {
                    if (compilation._lazyAssembly is null) {
                        compilation._lazyAssembly = assemblySymbol;
                        Debug.Assert(ReferenceEquals(compilation._referenceManager, this));
                    }
                }
            }
        }

        private void InitializeAssemblyReuseData(
            AssemblySymbol assemblySymbol,
            ImmutableArray<AssemblySymbol> referencedAssemblies,
            ImmutableArray<UnifiedAssembly<AssemblySymbol>> unifiedAssemblies) {
            AssertBound();

            if (corAssemblyOpt is not null)
                assemblySymbol.SetCorLibrary(corAssemblyOpt);
            else
                assemblySymbol.SetCorLibraryInternal(corLibrary);

            var sourceModuleReferences = new ModuleReferences<AssemblySymbol>(
                referencedAssemblies.SelectAsArray(a => a.identity),
                referencedAssemblies,
                unifiedAssemblies
            );

            assemblySymbol.modules[0].SetReferences(sourceModuleReferences);

            var assemblyModules = assemblySymbol.modules;
            var referencedModulesReferences = this.referencedModulesReferences;
            Debug.Assert(assemblyModules.Length == referencedModulesReferences.Length + 1);

            for (var i = 1; i < assemblyModules.Length; i++)
                assemblyModules[i].SetReferences(referencedModulesReferences[i - 1]);
        }

        private bool CreateAndSetSourceAssemblyFullBind(Compilation compilation) {
            var resolutionDiagnostics = BelteDiagnosticQueue.GetInstance();
            var assemblyReferencesBySimpleName = PooledDictionary<string, List<ReferencedAssemblyIdentity>>.GetInstance();

            // TODO
            var supersedeLowerVersions = true;

            try {
                var referenceMap = ResolveMetadataReferences(
                    compilation,
                    assemblyReferencesBySimpleName,
                    out var explicitReferences,
                    out var boundReferenceDirectiveMap,
                    out var boundReferenceDirectives,
                    out var referencedAssemblies,
                    out var modules,
                    resolutionDiagnostics
                );

                var assemblyBeingBuiltData = new AssemblyDataForAssemblyBeingBuilt(
                    new AssemblyIdentity(name: simpleAssemblyName, noThrow: true),
                    referencedAssemblies,
                    modules
                );

                var explicitAssemblyData = referencedAssemblies.Insert(0, assemblyBeingBuiltData);

                var bindingResult = Bind(
                    explicitAssemblyData,
                    modules,
                    explicitReferences,
                    referenceMap,
                    compilation.metadataReferenceResolver,
                    MetadataImportOptions.All,
                    supersedeLowerVersions,
                    assemblyReferencesBySimpleName,
                    out var allAssemblyData,
                    out var implicitlyResolvedReferences,
                    out var implicitlyResolvedReferenceMap,
                    resolutionDiagnostics,
                    out var hasCircularReference,
                    out var corLibraryIndex
                );

                Debug.Assert(bindingResult.Length == allAssemblyData.Length);

                var references = explicitReferences.AddRange(implicitlyResolvedReferences);
                referenceMap = referenceMap.AddRange(implicitlyResolvedReferenceMap);

                BuildReferencedAssembliesAndModulesMaps(
                    bindingResult,
                    references,
                    referenceMap,
                    modules.Length,
                    referencedAssemblies.Length,
                    assemblyReferencesBySimpleName,
                    supersedeLowerVersions,
                    out var referencedAssembliesMap,
                    out var referencedModulesMap,
                    out var aliasesOfReferencedAssemblies,
                    out var mergedAssemblyReferencesMapOpt
                );

                var newSymbols = new List<int>();

                for (var i = 1; i < bindingResult.Length; i++) {
                    ref var bound = ref bindingResult[i];

                    if (bound.assemblySymbol is null) {
                        bound.assemblySymbol = ((AssemblyDataForMetadataOrCompilation)allAssemblyData[i])
                            .CreateAssemblySymbol();

                        newSymbols.Add(i);
                    }

                    Debug.Assert(allAssemblyData[i].isLinked == bound.assemblySymbol.isLinked);
                }

                var assemblySymbol = new SourceAssemblySymbol(
                    compilation,
                    simpleAssemblyName,
                    compilation.MakeSourceModuleName(),
                    netModules: modules
                );

                AssemblySymbol corLibrary;

                if (corLibraryIndex == 0)
                    corLibrary = assemblySymbol;
                else if (corLibraryIndex > 0)
                    corLibrary = bindingResult[corLibraryIndex].assemblySymbol;
                else
                    corLibrary = null;

                if (corLibrary is not null) {
                    // In a reuse scenario this could already be set
                    if (corLibrary.corLibrary is null)
                        corLibrary.SetCorLibraryInternal(this.corLibrary);

                    if ((object)corLibrary != assemblySymbol)
                        assemblySymbol.SetCorLibrary(corLibrary);
                } else {
                    assemblySymbol.SetCorLibraryInternal(this.corLibrary);
                }

                Dictionary<AssemblyIdentity, MissingAssemblySymbol> missingAssemblies = null;
                var totalReferencedAssemblyCount = allAssemblyData.Length - 1;

                SetupReferencesForSourceAssembly(
                    assemblySymbol,
                    modules,
                    totalReferencedAssemblyCount,
                    bindingResult,
                    ref missingAssemblies,
                    out var moduleReferences
                );

                if (newSymbols.Count > 0) {
                    if (hasCircularReference)
                        bindingResult[0].assemblySymbol = assemblySymbol;

                    InitializeNewSymbols(newSymbols, assemblySymbol, allAssemblyData, bindingResult, missingAssemblies);
                }

                if (compilation._lazyAssembly is null) {
                    lock (SymbolCacheAndReferenceManagerStateGuard) {
                        if (compilation._lazyAssembly is null) {
                            if (isBound)
                                return false;

                            UpdateSymbolCacheNoLock(newSymbols, allAssemblyData, bindingResult);

                            InitializeNoLock(
                                referencedAssembliesMap,
                                referencedModulesMap,
                                boundReferenceDirectiveMap,
                                boundReferenceDirectives,
                                explicitReferences,
                                // implicitReferenceResolutions,
                                hasCircularReference,
                                resolutionDiagnostics,
                                ReferenceEquals(corLibrary, assemblySymbol) ? null : corLibrary,
                                modules,
                                moduleReferences,
                                assemblySymbol.sourceModule.referencedAssemblySymbols,
                                aliasesOfReferencedAssemblies,
                                assemblySymbol.sourceModule.GetUnifiedAssemblies(),
                                mergedAssemblyReferencesMapOpt
                            );

                            Debug.Assert(ReferenceEquals(compilation._referenceManager, this) || hasCircularReference);
                            compilation._referenceManager = this;
                            compilation._lazyAssembly = assemblySymbol;
                        }
                    }
                }

                return true;
            } finally {
                resolutionDiagnostics.Free();
                assemblyReferencesBySimpleName.Free();
            }
        }

        internal void InitializeNoLock(
            Dictionary<MetadataReference, int> referencedAssembliesMap,
            Dictionary<MetadataReference, int> referencedModulesMap,
            IDictionary<(string, string), MetadataReference> boundReferenceDirectiveMap,
            ImmutableArray<MetadataReference> directiveReferences,
            ImmutableArray<MetadataReference> explicitReferences,
            // ImmutableDictionary<AssemblyIdentity, PortableExecutableReference?> implicitReferenceResolutions,
            bool containsCircularReferences,
            BelteDiagnosticQueue diagnostics,
            AssemblySymbol corAssemblyOpt,
            ImmutableArray<PEModule> referencedModules,
            ImmutableArray<ModuleReferences<AssemblySymbol>> referencedModulesReferences,
            ImmutableArray<AssemblySymbol> referencedAssemblies,
            ImmutableArray<ImmutableArray<string>> aliasesOfReferencedAssemblies,
            ImmutableArray<UnifiedAssembly<AssemblySymbol>> unifiedAssemblies,
            Dictionary<MetadataReference, ImmutableArray<MetadataReference>>? mergedAssemblyReferencesMapOpt) {
            AssertUnbound();

            Debug.Assert(referencedModules.Length == referencedModulesReferences.Length);
            Debug.Assert(referencedModules.Length == referencedModulesMap.Count);
            Debug.Assert(referencedAssemblies.Length == aliasesOfReferencedAssemblies.Length);

            _lazyReferencedAssembliesMap = referencedAssembliesMap;
            _lazyReferencedModuleIndexMap = referencedModulesMap;

            _lazyDiagnostics = new BelteDiagnosticQueue();
            _lazyDiagnostics.PushRange(diagnostics);

            _lazyReferenceDirectiveMap = boundReferenceDirectiveMap;
            _lazyDirectiveReferences = directiveReferences;
            _lazyExplicitReferences = explicitReferences;
            // _lazyImplicitReferenceResolutions = implicitReferenceResolutions;

            _lazyCorAssemblyOpt = corAssemblyOpt;
            _lazyReferencedModules = referencedModules;
            _lazyReferencedModulesReferences = referencedModulesReferences;
            _lazyReferencedAssemblies = referencedAssemblies;
            _lazyAliasesOfReferencedAssemblies = aliasesOfReferencedAssemblies;
            _lazyMergedAssemblyReferencesMap = mergedAssemblyReferencesMapOpt?.ToImmutableDictionary() ?? [];
            _lazyUnifiedAssemblies = unifiedAssemblies;
            _lazyHasCircularReference = containsCircularReferences.ToThreeState();

            Interlocked.Exchange(ref _isBound, 1);
        }

        private static void UpdateSymbolCacheNoLock(
            List<int> newSymbols,
            ImmutableArray<AssemblyData> assemblies,
            BoundInputAssembly[] bindingResult) {
            foreach (var i in newSymbols) {
                ref var current = ref bindingResult[i];
                Debug.Assert(current.assemblySymbol is not null);

                // var compilationData = assemblies[i] as AssemblyDataForCompilation;
                // if (compilationData != null) {
                //     compilationData.Compilation.CacheRetargetingAssemblySymbolNoLock(current.AssemblySymbol);
                // } else {
                var fileData = (AssemblyDataForFile)assemblies[i];
                fileData.cachedSymbols.Add((PEAssemblySymbol)current.assemblySymbol);
                // }
            }
        }

        private static void InitializeNewSymbols(
            List<int> newSymbols,
            SourceAssemblySymbol sourceAssembly,
            ImmutableArray<AssemblyData> assemblies,
            BoundInputAssembly[] bindingResult,
            Dictionary<AssemblyIdentity, MissingAssemblySymbol>? missingAssemblies) {
            Debug.Assert(newSymbols.Count > 0);

            var corLibrary = sourceAssembly.corAssembly;

            foreach (var i in newSymbols) {
                // var compilationData = assemblies[i] as AssemblyDataForCompilation;

                // if (compilationData != null) {
                //     SetupReferencesForRetargetingAssembly(bindingResult, ref bindingResult[i], ref missingAssemblies, sourceAssemblyDebugOnly: sourceAssembly);
                // } else {
                var fileData = (AssemblyDataForFile)assemblies[i];
                SetupReferencesForFileAssembly(
                    fileData,
                    bindingResult,
                    ref bindingResult[i],
                    ref missingAssemblies,
                    sourceAssemblyDebugOnly: sourceAssembly
                );
                // }
            }

            var linkedReferencedAssembliesBuilder = ArrayBuilder<AssemblySymbol>.GetInstance();
            var noPiaResolutionAssemblies = sourceAssembly.modules[0].referencedAssemblySymbols;

            foreach (var i in newSymbols) {
                ref var currentBindingResult = ref bindingResult[i];
                Debug.Assert(currentBindingResult.assemblySymbol is not null);
                Debug.Assert(currentBindingResult.referenceBinding is not null);

                if (assemblies[i].containsNoPiaLocalTypes)
                    currentBindingResult.assemblySymbol.SetNoPiaResolutionAssemblies(noPiaResolutionAssemblies);

                linkedReferencedAssembliesBuilder.Clear();

                if (assemblies[i].isLinked)
                    linkedReferencedAssembliesBuilder.Add(currentBindingResult.assemblySymbol);

                foreach (var referenceBinding in currentBindingResult.referenceBinding) {
                    if (referenceBinding.isBound &&
                        assemblies[referenceBinding.definitionIndex].isLinked) {
                        var linkedAssemblySymbol = bindingResult[referenceBinding.definitionIndex].assemblySymbol;
                        Debug.Assert(linkedAssemblySymbol is not null);
                        linkedReferencedAssembliesBuilder.Add(linkedAssemblySymbol);
                    }
                }

                if (linkedReferencedAssembliesBuilder.Count > 0) {
                    linkedReferencedAssembliesBuilder.RemoveDuplicates();
                    currentBindingResult.assemblySymbol.SetLinkedReferencedAssemblies(
                        linkedReferencedAssembliesBuilder.ToImmutable()
                    );
                }

                // TODO It should always be null, but for some reason isn't
                if (currentBindingResult.assemblySymbol.corAssembly is null)
                    currentBindingResult.assemblySymbol.SetCorLibrary(corLibrary);
            }

            linkedReferencedAssembliesBuilder.Free();

            if (missingAssemblies is not null) {
                foreach (var missingAssembly in missingAssemblies.Values)
                    missingAssembly.SetCorLibrary(corLibrary);
            }
        }

        private static void SetupReferencesForFileAssembly(
            AssemblyDataForFile fileData,
            BoundInputAssembly[] bindingResult,
            ref BoundInputAssembly currentBindingResult,
            ref Dictionary<AssemblyIdentity, MissingAssemblySymbol>? missingAssemblies,
            SourceAssemblySymbol sourceAssemblyDebugOnly) {
            Debug.Assert(currentBindingResult.assemblySymbol is not null);
            Debug.Assert(currentBindingResult.referenceBinding is not null);
            var portableExecutableAssemblySymbol = (PEAssemblySymbol)currentBindingResult.assemblySymbol;

            var modules = portableExecutableAssemblySymbol.modules;
            var moduleCount = modules.Length;
            var refsUsed = 0;

            for (var j = 0; j < moduleCount; j++) {
                var moduleReferenceCount = fileData.assembly.moduleReferenceCounts[j];
                var identities = new AssemblyIdentity[moduleReferenceCount];
                var symbols = new AssemblySymbol[moduleReferenceCount];

                fileData.assemblyReferences.CopyTo(refsUsed, identities, 0, moduleReferenceCount);

                ArrayBuilder<UnifiedAssembly<AssemblySymbol>> unifiedAssemblies = null;

                for (var k = 0; k < moduleReferenceCount; k++) {
                    var boundReference = currentBindingResult.referenceBinding[refsUsed + k];

                    if (boundReference.isBound)
                        symbols[k] = GetAssemblyDefinitionSymbol(bindingResult, boundReference, ref unifiedAssemblies);
                    else
                        symbols[k] = GetOrAddMissingAssemblySymbol(identities[k], ref missingAssemblies);
                }

                var moduleReferences = new ModuleReferences<AssemblySymbol>(
                    identities.AsImmutableOrNull(),
                    symbols.AsImmutableOrNull(),
                    unifiedAssemblies.AsImmutableOrEmpty()
                );

                modules[j].SetReferences(moduleReferences, sourceAssemblyDebugOnly);
                refsUsed += moduleReferenceCount;
            }
        }

        private static void SetupReferencesForSourceAssembly(
            SourceAssemblySymbol sourceAssembly,
            ImmutableArray<PEModule> modules,
            int totalReferencedAssemblyCount,
            BoundInputAssembly[] bindingResult,
            ref Dictionary<AssemblyIdentity, MissingAssemblySymbol>? missingAssemblies,
            out ImmutableArray<ModuleReferences<AssemblySymbol>> moduleReferences) {
            var moduleSymbols = sourceAssembly.modules;
            Debug.Assert(moduleSymbols.Length == 1 + modules.Length);

            var moduleReferencesBuilder = (moduleSymbols.Length > 1)
                ? ArrayBuilder<ModuleReferences<AssemblySymbol>>.GetInstance()
                : null;

            var refsUsed = 0;

            for (var moduleIndex = 0; moduleIndex < moduleSymbols.Length; moduleIndex++) {
                var refsCount = (moduleIndex == 0)
                    ? totalReferencedAssemblyCount
                    : modules[moduleIndex - 1].referencedAssemblies.Length;

                var identities = new AssemblyIdentity[refsCount];
                var symbols = new AssemblySymbol[refsCount];

                ArrayBuilder<UnifiedAssembly<AssemblySymbol>>? unifiedAssemblies = null;

                for (var k = 0; k < refsCount; k++) {
                    Debug.Assert(bindingResult[0].referenceBinding is not null);
                    var boundReference = bindingResult[0].referenceBinding[refsUsed + k];
                    Debug.Assert(boundReference.referenceIdentity is object);

                    if (boundReference.isBound) {
                        symbols[k] = GetAssemblyDefinitionSymbol(bindingResult, boundReference, ref unifiedAssemblies);
                    } else {
                        symbols[k] = GetOrAddMissingAssemblySymbol(
                            boundReference.referenceIdentity,
                            ref missingAssemblies
                        );
                    }

                    identities[k] = boundReference.referenceIdentity;
                }

                var references = new ModuleReferences<AssemblySymbol>(
                    identities.AsImmutableOrNull(),
                    symbols.AsImmutableOrNull(),
                    unifiedAssemblies.AsImmutableOrEmpty()
                );

                if (moduleIndex > 0)
                    moduleReferencesBuilder.Add(references);

                moduleSymbols[moduleIndex].SetReferences(references, sourceAssembly);
                refsUsed += refsCount;
            }

            moduleReferences = moduleReferencesBuilder.ToImmutableOrEmptyAndFree();
        }

        private static MissingAssemblySymbol GetOrAddMissingAssemblySymbol(
            AssemblyIdentity assemblyIdentity,
            ref Dictionary<AssemblyIdentity, MissingAssemblySymbol>? missingAssemblies) {
            MissingAssemblySymbol? missingAssembly;

            if (missingAssemblies is null)
                missingAssemblies = [];
            else if (missingAssemblies.TryGetValue(assemblyIdentity, out missingAssembly))
                return missingAssembly;

            missingAssembly = new MissingAssemblySymbol(assemblyIdentity);
            missingAssemblies.Add(assemblyIdentity, missingAssembly);

            return missingAssembly;
        }

        private static AssemblySymbol GetAssemblyDefinitionSymbol(
            BoundInputAssembly[] bindingResult,
            AssemblyReferenceBinding referenceBinding,
            ref ArrayBuilder<UnifiedAssembly<AssemblySymbol>>? unifiedAssemblies) {
            Debug.Assert(referenceBinding.isBound);
            Debug.Assert(referenceBinding.referenceIdentity is not null);
            var assembly = bindingResult[referenceBinding.definitionIndex].assemblySymbol;
            Debug.Assert(assembly is not null);

            if (referenceBinding.versionDifference != 0) {
                unifiedAssemblies ??= new ArrayBuilder<UnifiedAssembly<AssemblySymbol>>();
                unifiedAssemblies.Add(
                    new UnifiedAssembly<AssemblySymbol>(assembly, referenceBinding.referenceIdentity)
                );
            }

            return assembly;
        }

        private static void BuildReferencedAssembliesAndModulesMaps(
            BoundInputAssembly[] bindingResult,
            ImmutableArray<MetadataReference> references,
            ImmutableArray<ResolvedReference> referenceMap,
            int referencedModuleCount,
            int explicitlyReferencedAssemblyCount,
            IReadOnlyDictionary<string, List<ReferencedAssemblyIdentity>> assemblyReferencesBySimpleName,
            bool supersedeLowerVersions,
            out Dictionary<MetadataReference, int> referencedAssembliesMap,
            out Dictionary<MetadataReference, int> referencedModulesMap,
            out ImmutableArray<ImmutableArray<string>> aliasesOfReferencedAssemblies,
            out Dictionary<MetadataReference, ImmutableArray<MetadataReference>>? mergedAssemblyReferencesMapOpt) {
            referencedAssembliesMap = new Dictionary<MetadataReference, int>(referenceMap.Length);
            referencedModulesMap = new Dictionary<MetadataReference, int>(referencedModuleCount);
            var aliasesOfReferencedAssembliesBuilder = ArrayBuilder<ImmutableArray<string>>
                .GetInstance(referenceMap.Length - referencedModuleCount);

            var hasRecursiveAliases = false;

            mergedAssemblyReferencesMapOpt = null;

            for (var i = 0; i < referenceMap.Length; i++) {
                if (referenceMap[i].isSkipped)
                    continue;

                if (referenceMap[i].kind == MetadataImageKind.Module) {
                    var moduleIndex = 1 + referenceMap[i].index;
                    referencedModulesMap.Add(references[i], moduleIndex);
                } else {
                    var assemblyIndex = referenceMap[i].index;
                    Debug.Assert(aliasesOfReferencedAssembliesBuilder.Count == assemblyIndex);

                    var reference = references[i];
                    referencedAssembliesMap.Add(reference, assemblyIndex);
                    aliasesOfReferencedAssembliesBuilder.Add(referenceMap[i].aliasesOpt);

                    if (!referenceMap[i].mergedReferences.IsEmpty) {
                        (mergedAssemblyReferencesMapOpt ??= []).Add(reference, referenceMap[i].mergedReferences);
                    }

                    hasRecursiveAliases |= !referenceMap[i].recursiveAliasesOpt.IsDefault;
                }
            }

            if (hasRecursiveAliases)
                PropagateRecursiveAliases(bindingResult, referenceMap, aliasesOfReferencedAssembliesBuilder);

            Debug.Assert(!aliasesOfReferencedAssembliesBuilder.Any(a => a.IsDefault));

            if (supersedeLowerVersions) {
                foreach (var assemblyReference in assemblyReferencesBySimpleName) {
                    for (var i = 1; i < assemblyReference.Value.Count; i++) {
                        var assemblyIndex = assemblyReference.Value[i]
                            .GetAssemblyIndex(explicitlyReferencedAssemblyCount);

                        aliasesOfReferencedAssembliesBuilder[assemblyIndex] = SupersededAlias;
                    }
                }
            }

            aliasesOfReferencedAssemblies = aliasesOfReferencedAssembliesBuilder.ToImmutableAndFree();
        }

        private static void PropagateRecursiveAliases(
            BoundInputAssembly[] bindingResult,
            ImmutableArray<ResolvedReference> referenceMap,
            ArrayBuilder<ImmutableArray<string>> aliasesOfReferencedAssembliesBuilder) {
            var assemblyIndicesToProcess = ArrayBuilder<int>.GetInstance();
            var visitedAssemblies = BitVector.Create(bindingResult.Length);

            Debug.Assert(bindingResult.Length == aliasesOfReferencedAssembliesBuilder.Count + 1);

            foreach (var reference in referenceMap) {
                if (!reference.isSkipped && !reference.recursiveAliasesOpt.IsDefault) {
                    var recursiveAliases = reference.recursiveAliasesOpt;

                    Debug.Assert(reference.kind == MetadataImageKind.Assembly);
                    visitedAssemblies.Clear();

                    Debug.Assert(assemblyIndicesToProcess.Count == 0);
                    assemblyIndicesToProcess.Add(reference.index);

                    while (assemblyIndicesToProcess.Count > 0) {
                        var assemblyIndex = assemblyIndicesToProcess.Pop();
                        visitedAssemblies[assemblyIndex] = true;

                        aliasesOfReferencedAssembliesBuilder[assemblyIndex] = MergedAliases
                            .Merge(aliasesOfReferencedAssembliesBuilder[assemblyIndex], recursiveAliases);

                        var referenceBinding = bindingResult[assemblyIndex + 1].referenceBinding;
                        Debug.Assert(referenceBinding is object);

                        foreach (var binding in referenceBinding) {
                            if (binding.isBound) {
                                var dependentAssemblyIndex = binding.definitionIndex - 1;

                                if (!visitedAssemblies[dependentAssemblyIndex])
                                    assemblyIndicesToProcess.Add(dependentAssemblyIndex);
                            }
                        }
                    }
                }
            }

            for (var i = 0; i < aliasesOfReferencedAssembliesBuilder.Count; i++) {
                if (aliasesOfReferencedAssembliesBuilder[i].IsDefault)
                    aliasesOfReferencedAssembliesBuilder[i] = [];
            }

            assemblyIndicesToProcess.Free();
        }

        private BoundInputAssembly[] Bind(
            ImmutableArray<AssemblyData> explicitAssemblies,
            ImmutableArray<PEModule> explicitModules,
            ImmutableArray<MetadataReference> explicitReferences,
            ImmutableArray<ResolvedReference> explicitReferenceMap,
            MetadataReferenceResolver resolverOpt,
            MetadataImportOptions importOptions,
            bool supersedeLowerVersions,
            Dictionary<string, List<ReferencedAssemblyIdentity>> assemblyReferencesBySimpleName,
            out ImmutableArray<AssemblyData> allAssemblies,
            out ImmutableArray<MetadataReference> implicitlyResolvedReferences,
            out ImmutableArray<ResolvedReference> implicitlyResolvedReferenceMap,
            BelteDiagnosticQueue resolutionDiagnostics,
            out bool hasCircularReference,
            out int corLibraryIndex) {
            Debug.Assert(explicitAssemblies[0] is AssemblyDataForAssemblyBeingBuilt);
            Debug.Assert(explicitReferences.Length == explicitReferenceMap.Length);

            var referenceBindings = ArrayBuilder<AssemblyReferenceBinding[]>.GetInstance();
            var explicitAssembliesMap = Pool.Allocate();
            explicitAssembliesMap.EnsureCapacity(explicitAssemblies.Length);

            try {
                for (var i = 0; i < explicitAssemblies.Length; i++)
                    explicitAssembliesMap.Add(explicitAssemblies[i].identity.name, (explicitAssemblies[i], i));

                for (var i = 0; i < explicitAssemblies.Length; i++) {
                    referenceBindings.Add(
                        explicitAssemblies[i].BindAssemblyReferences(explicitAssembliesMap, identityComparer)
                    );
                }

                if (resolverOpt?.resolveMissingAssemblies == true) {
                    ResolveAndBindMissingAssemblies(
                        explicitAssemblies,
                        explicitAssembliesMap,
                        explicitModules,
                        explicitReferences,
                        explicitReferenceMap,
                        resolverOpt,
                        importOptions,
                        supersedeLowerVersions,
                        referenceBindings,
                        assemblyReferencesBySimpleName,
                        out allAssemblies,
                        out implicitlyResolvedReferences,
                        out implicitlyResolvedReferenceMap,
                        resolutionDiagnostics
                    );
                } else {
                    allAssemblies = explicitAssemblies;
                    implicitlyResolvedReferences = ImmutableArray<MetadataReference>.Empty;
                    implicitlyResolvedReferenceMap = ImmutableArray<ResolvedReference>.Empty;
                }

                Debug.Assert(referenceBindings.Count == allAssemblies.Length);

                hasCircularReference = CheckCircularReference(referenceBindings);
                corLibraryIndex = IndexOfCorLibrary(explicitAssemblies, assemblyReferencesBySimpleName, supersedeLowerVersions);

                var boundInputs = new BoundInputAssembly[referenceBindings.Count];

                for (var i = 0; i < referenceBindings.Count; i++)
                    boundInputs[i].referenceBinding = referenceBindings[i];

                var candidateInputAssemblySymbols = new AssemblySymbol[allAssemblies.Length];

                if (!hasCircularReference) {
                    if (ReuseAssemblySymbolsWithNoPiaLocalTypes(
                        boundInputs,
                        candidateInputAssemblySymbols,
                        allAssemblies,
                        corLibraryIndex)) {
                        return boundInputs;
                    }
                }

                ReuseAssemblySymbols(boundInputs, candidateInputAssemblySymbols, allAssemblies, corLibraryIndex);

                return boundInputs;
            } finally {
                explicitAssembliesMap.Clear();
                Pool.Free(explicitAssembliesMap);

                referenceBindings.Free();
            }
        }

        private void ResolveAndBindMissingAssemblies(
            ImmutableArray<AssemblyData> explicitAssemblies,
            MultiDictionary<string, (AssemblyData DefinitionData, int DefinitionIndex)> explicitAssembliesMap,
            ImmutableArray<PEModule> explicitModules,
            ImmutableArray<MetadataReference> explicitReferences,
            ImmutableArray<ResolvedReference> explicitReferenceMap,
            MetadataReferenceResolver resolver,
            MetadataImportOptions importOptions,
            bool supersedeLowerVersions,
            ArrayBuilder<AssemblyReferenceBinding[]> referenceBindings,
            Dictionary<string, List<ReferencedAssemblyIdentity>> assemblyReferencesBySimpleName,
            out ImmutableArray<AssemblyData> allAssemblies,
            out ImmutableArray<MetadataReference> metadataReferences,
            out ImmutableArray<ResolvedReference> resolvedReferences,
            // ref ImmutableDictionary<AssemblyIdentity, PortableExecutableReference?> implicitReferenceResolutions,
            BelteDiagnosticQueue resolutionDiagnostics) {
            Debug.Assert(explicitAssemblies[0] is AssemblyDataForAssemblyBeingBuilt);
            Debug.Assert(referenceBindings.Count == explicitAssemblies.Length);
            Debug.Assert(explicitReferences.Length == explicitReferenceMap.Length);

            var totalReferencedAssemblyCount = explicitAssemblies.Length - 1;
            var implicitAssemblies = ArrayBuilder<AssemblyData>.GetInstance();
            var resolutionFailures = PooledHashSet<AssemblyIdentity>.GetInstance();
            var metadataReferencesBuilder = ArrayBuilder<MetadataReference>.GetInstance();

            Dictionary<MetadataReference, MergedAliases> lazyAliasMap = null;

            var referenceBindingsToProcess = ArrayBuilder<(MetadataReference, ArraySegment<AssemblyReferenceBinding>)>
                .GetInstance();

            GetInitialReferenceBindingsToProcess(
                explicitModules,
                explicitReferences,
                explicitReferenceMap,
                referenceBindings,
                totalReferencedAssemblyCount,
                referenceBindingsToProcess
            );

            var explicitAssemblyCount = explicitAssemblies.Length;
            MultiDictionary<string, (AssemblyData DefinitionData, int DefinitionIndex)> implicitAssembliesMap = null;

            try {
                while (referenceBindingsToProcess.Count > 0) {
                    var (requestingReference, bindings) = referenceBindingsToProcess.Pop();

                    foreach (var binding in bindings) {
                        if (binding.isBound)
                            continue;

                        Debug.Assert(binding.referenceIdentity is not null);

                        if (!TryResolveMissingReference(
                            requestingReference,
                            binding.referenceIdentity,
                            // ref implicitReferenceResolutions,
                            resolver,
                            resolutionDiagnostics,
                            out var resolvedAssemblyIdentity,
                            out var resolvedAssemblyMetadata,
                            out var resolvedReference)) {
                            resolutionFailures.Add(binding.referenceIdentity);
                            continue;
                        }

                        resolutionFailures.Remove(binding.referenceIdentity);

                        var index = explicitAssemblyCount - 1 + metadataReferencesBuilder.Count;

                        var existingReference = TryAddAssembly(
                            resolvedAssemblyIdentity,
                            resolvedReference,
                            index,
                            resolutionDiagnostics,
                            null,
                            assemblyReferencesBySimpleName,
                            supersedeLowerVersions
                        );

                        if (existingReference is not null) {
                            MergeReferenceProperties(
                                existingReference,
                                resolvedReference,
                                resolutionDiagnostics,
                                ref lazyAliasMap
                            );

                            continue;
                        }

                        metadataReferencesBuilder.Add(resolvedReference);

                        var data = CreateAssemblyDataForResolvedMissingAssembly(
                            resolvedAssemblyMetadata,
                            resolvedReference,
                            importOptions
                        );

                        implicitAssemblies.Add(data);

                        var referenceBinding = data.BindAssemblyReferences(explicitAssembliesMap, identityComparer);
                        referenceBindings.Add(referenceBinding);

                        referenceBindingsToProcess.Push(
                            (resolvedReference, new ArraySegment<AssemblyReferenceBinding>(referenceBinding))
                        );
                    }
                }

                // foreach (var assemblyIdentity in resolutionFailures)
                //     implicitReferenceResolutions = implicitReferenceResolutions.Add(assemblyIdentity, null);

                if (implicitAssemblies.Count == 0) {
                    Debug.Assert(lazyAliasMap is null);

                    resolvedReferences = [];
                    metadataReferences = [];
                    allAssemblies = explicitAssemblies;
                    return;
                }

                implicitAssembliesMap = Pool.Allocate();
                implicitAssembliesMap.EnsureCapacity(implicitAssemblies.Count);

                for (var i = 0; i < implicitAssemblies.Count; i++) {
                    implicitAssembliesMap.Add(
                        implicitAssemblies[i].identity.name,
                        (implicitAssemblies[i], explicitAssemblyCount + i)
                    );
                }

                allAssemblies = explicitAssemblies.AddRange(implicitAssemblies);

                for (var bindingsIndex = 0; bindingsIndex < referenceBindings.Count; bindingsIndex++) {
                    var referenceBinding = referenceBindings[bindingsIndex];

                    for (var i = 0; i < referenceBinding.Length; i++) {
                        var binding = referenceBinding[i];

                        if (binding.isBound)
                            continue;

                        Debug.Assert(binding.referenceIdentity is not null);

                        referenceBinding[i] = ResolveReferencedAssembly(
                            binding.referenceIdentity,
                            implicitAssembliesMap,
                            resolveAgainstAssemblyBeingBuilt: false,
                            identityComparer
                        );
                    }
                }

                UpdateBindingsOfAssemblyBeingBuilt(referenceBindings, explicitAssemblyCount, implicitAssemblies);

                metadataReferences = metadataReferencesBuilder.ToImmutable();

                resolvedReferences = ToResolvedAssemblyReferences(
                    metadataReferences,
                    lazyAliasMap,
                    explicitAssemblyCount
                );
            } finally {
                if (implicitAssembliesMap is not null) {
                    implicitAssembliesMap.Clear();
                    Pool.Free(implicitAssembliesMap);
                }

                implicitAssemblies.Free();
                referenceBindingsToProcess.Free();
                metadataReferencesBuilder.Free();
                resolutionFailures.Free();
            }
        }

        private void GetInitialReferenceBindingsToProcess(
            ImmutableArray<PEModule> explicitModules,
            ImmutableArray<MetadataReference> explicitReferences,
            ImmutableArray<ResolvedReference> explicitReferenceMap,
            ArrayBuilder<AssemblyReferenceBinding[]> referenceBindings,
            int totalReferencedAssemblyCount,
            ArrayBuilder<(MetadataReference, ArraySegment<AssemblyReferenceBinding>)> result) {
            Debug.Assert(result.Count == 0);

            var explicitModuleToReferenceMap = CalculateModuleToReferenceMap(explicitModules, explicitReferenceMap);

            var bindingsOfAssemblyBeingBuilt = referenceBindings[0];
            var bindingIndex = totalReferencedAssemblyCount;

            for (var moduleIndex = 0; moduleIndex < explicitModules.Length; moduleIndex++) {
                var moduleReference = explicitReferences[explicitModuleToReferenceMap[moduleIndex]];
                var moduleBindingsCount = explicitModules[moduleIndex].referencedAssemblies.Length;

                result.Add(
                    (moduleReference,
                     new ArraySegment<AssemblyReferenceBinding>(
                        bindingsOfAssemblyBeingBuilt,
                        bindingIndex,
                        moduleBindingsCount
                    ))
                );

                bindingIndex += moduleBindingsCount;
            }

            Debug.Assert(bindingIndex == bindingsOfAssemblyBeingBuilt.Length);

            for (var referenceIndex = 0; referenceIndex < explicitReferenceMap.Length; referenceIndex++) {
                var explicitReferenceMapping = explicitReferenceMap[referenceIndex];

                if (explicitReferenceMapping.isSkipped || explicitReferenceMapping.kind == MetadataImageKind.Module)
                    continue;

                result.Add(
                    (explicitReferences[referenceIndex],
                     new ArraySegment<AssemblyReferenceBinding>(referenceBindings[explicitReferenceMapping.index + 1]))
                );
            }

            Debug.Assert(result.Count == explicitModules.Length + totalReferencedAssemblyCount);
        }

        private static ImmutableArray<int> CalculateModuleToReferenceMap(
            ImmutableArray<PEModule> modules,
            ImmutableArray<ResolvedReference> resolvedReferences) {
            if (modules.Length == 0)
                return [];

            var result = ArrayBuilder<int>.GetInstance(modules.Length);
            result.ZeroInit(modules.Length);

            for (var i = 0; i < resolvedReferences.Length; i++) {
                var resolvedReference = resolvedReferences[i];

                if (!resolvedReference.isSkipped && resolvedReference.kind == MetadataImageKind.Module)
                    result[resolvedReference.index] = i;
            }

            return result.ToImmutableAndFree();
        }

        private bool TryResolveMissingReference(
            MetadataReference requestingReference,
            AssemblyIdentity referenceIdentity,
            // ref ImmutableDictionary<AssemblyIdentity, PortableExecutableReference> implicitReferenceResolutions,
            MetadataReferenceResolver resolver,
            BelteDiagnosticQueue resolutionDiagnostics,
            out AssemblyIdentity resolvedAssemblyIdentity,
            out AssemblyMetadata resolvedAssemblyMetadata,
            out PortableExecutableReference resolvedReference) {
            resolvedAssemblyIdentity = null;
            resolvedAssemblyMetadata = null;
            var isNewlyResolvedReference = false;

            // if (!implicitReferenceResolutions.TryGetValue(referenceIdentity, out resolvedReference)) {
            resolvedReference = resolver.ResolveMissingAssembly(requestingReference, referenceIdentity);
            isNewlyResolvedReference = true;
            // }

            if (resolvedReference is null)
                return false;

            resolvedAssemblyMetadata = GetAssemblyMetadata(resolvedReference, resolutionDiagnostics);

            if (resolvedAssemblyMetadata is null)
                return false;

            var resolvedAssembly = resolvedAssemblyMetadata.GetAssembly();
            Debug.Assert(resolvedAssembly is not null);

            if (isNewlyResolvedReference &&
                identityComparer.Compare(referenceIdentity, resolvedAssembly.identity)
                    == AssemblyIdentityComparer.ComparisonResult.NotEquivalent) {

                return false;
            }

            resolvedAssemblyIdentity = resolvedAssembly.identity;
            // implicitReferenceResolutions = implicitReferenceResolutions.Add(referenceIdentity, resolvedReference);
            return true;
        }

        internal AssemblyMetadata? GetAssemblyMetadata(
            PortableExecutableReference peReference,
            BelteDiagnosticQueue diagnostics) {
            var metadata = GetMetadata(peReference, null, diagnostics);
            Debug.Assert(metadata is not null || diagnostics.AnyErrors());

            if (metadata is null)
                return null;

            var assemblyMetadata = metadata as AssemblyMetadata;

            if (assemblyMetadata?.IsValidAssembly() != true) {
                // diagnostics.Add(MessageProvider.CreateDiagnostic(MessageProvider.ERR_MetadataFileNotAssembly, Location.None, peReference.Display ?? ""));
                // return null;
                // TODO InvalidReference?
                throw ExceptionUtilities.Unreachable();
            }

            return assemblyMetadata;
        }

        private static ImmutableArray<ResolvedReference> ToResolvedAssemblyReferences(
            ImmutableArray<MetadataReference> references,
            Dictionary<MetadataReference, MergedAliases>? propertyMapOpt,
            int explicitAssemblyCount) {
            var result = ArrayBuilder<ResolvedReference>.GetInstance(references.Length);
            for (var i = 0; i < references.Length; i++) {
                result.Add(GetResolvedReferenceAndFreePropertyMapEntry(
                    references[i],
                    explicitAssemblyCount - 1 + i,
                    MetadataImageKind.Assembly,
                    propertyMapOpt
                ));
            }

            return result.ToImmutableAndFree();
        }

        private static void UpdateBindingsOfAssemblyBeingBuilt(
            ArrayBuilder<AssemblyReferenceBinding[]> referenceBindings,
            int explicitAssemblyCount,
            ArrayBuilder<AssemblyData> implicitAssemblies) {
            var referenceBindingsOfAssemblyBeingBuilt = referenceBindings[0];

            var bindingsOfAssemblyBeingBuilt = ArrayBuilder<AssemblyReferenceBinding>
                .GetInstance(referenceBindingsOfAssemblyBeingBuilt.Length + implicitAssemblies.Count);

            bindingsOfAssemblyBeingBuilt.AddRange(referenceBindingsOfAssemblyBeingBuilt, explicitAssemblyCount - 1);

            for (var i = 0; i < implicitAssemblies.Count; i++) {
                bindingsOfAssemblyBeingBuilt.Add(
                    new AssemblyReferenceBinding(implicitAssemblies[i].identity, explicitAssemblyCount + i)
                );
            }

            bindingsOfAssemblyBeingBuilt.AddRange(
                referenceBindingsOfAssemblyBeingBuilt,
                explicitAssemblyCount - 1,
                referenceBindingsOfAssemblyBeingBuilt.Length - explicitAssemblyCount + 1
            );

            referenceBindings[0] = bindingsOfAssemblyBeingBuilt.ToArrayAndFree();
        }

        private AssemblyData CreateAssemblyDataForResolvedMissingAssembly(
            AssemblyMetadata assemblyMetadata,
            PortableExecutableReference peReference,
            MetadataImportOptions importOptions) {
            var assembly = assemblyMetadata.GetAssembly();
            Debug.Assert(assembly is not null);

            return CreateAssemblyDataForFile(
                assembly,
                assemblyMetadata.cachedSymbols,
                simpleAssemblyName,
                importOptions,
                peReference.properties.embedInteropTypes
            );
        }

        private static bool IsSuperseded(
            AssemblyIdentity identity,
            IReadOnlyDictionary<string, List<ReferencedAssemblyIdentity>> assemblyReferencesBySimpleName) {
            var value = assemblyReferencesBySimpleName[identity.name][0];
            Debug.Assert(value.identity is not null);
            return value.identity.version != identity.version;
        }

        private static int IndexOfCorLibrary(
            ImmutableArray<AssemblyData> assemblies,
            IReadOnlyDictionary<string, List<ReferencedAssemblyIdentity>> assemblyReferencesBySimpleName,
            bool supersedeLowerVersions) {
            ArrayBuilder<int> corLibraryCandidates = null;

            for (var i = 1; i < assemblies.Length; i++) {
                var assembly = assemblies[i];

                // TODO Eventually we might use this logic for the CorLibrary
                // For now, our "CorLibrary" assembly actually isn't the CorLibrary (thats a static singleton)
                // Instead, we are looking for an assembly that defines some WellKnownTypes that we need
                // So we will just try and find it based on a hardcoded name

                // if (!assembly.isLinked &&
                //     assembly.assemblyReferences.Length == 0 &&
                //     !assembly.containsNoPiaLocalTypes &&
                //     (!supersedeLowerVersions || !IsSuperseded(assembly.identity, assemblyReferencesBySimpleName))) {
                //     if (assembly.declaresTheObjectClass) {
                //         corLibraryCandidates ??= ArrayBuilder<int>.GetInstance();
                //         corLibraryCandidates.Add(i);
                //     }
                // }

                if (!assembly.isLinked &&
                    !assembly.containsNoPiaLocalTypes &&
                    (!supersedeLowerVersions || !IsSuperseded(assembly.identity, assemblyReferencesBySimpleName))) {
                    if (MetadataHelpers.IsCorLibraryName(assembly.identity.name)) {
                        corLibraryCandidates ??= ArrayBuilder<int>.GetInstance();
                        corLibraryCandidates.Add(i);
                    }
                }
            }

            if (corLibraryCandidates is not null) {
                if (corLibraryCandidates.Count == 1) {
                    var result = corLibraryCandidates[0];
                    corLibraryCandidates.Free();
                    return result;
                } else {
                    corLibraryCandidates.Free();
                }
            }

            if (assemblies.Length == 1 && assemblies[0].assemblyReferences.Length == 0)
                return 0;

            return -1;
        }

        private static bool CheckCircularReference(IReadOnlyList<AssemblyReferenceBinding[]> referenceBindings) {
            for (var i = 1; i < referenceBindings.Count; i++) {
                foreach (var index in referenceBindings[i]) {
                    if (index.boundToAssemblyBeingBuilt)
                        return true;
                }
            }

            return false;
        }

        private bool ReuseAssemblySymbolsWithNoPiaLocalTypes(
            BoundInputAssembly[] boundInputs,
            AssemblySymbol[] candidateInputAssemblySymbols,
            ImmutableArray<AssemblyData> assemblies,
            int corLibraryIndex) {
            var totalAssemblies = assemblies.Length;

            for (var i = 1; i < totalAssemblies; i++) {
                if (!assemblies[i].containsNoPiaLocalTypes)
                    continue;

                foreach (var candidateAssembly in assemblies[i].availableSymbols) {
                    if (IsLinked(candidateAssembly) != assemblies[i].isLinked)
                        continue;

                    var resolutionAssemblies = GetNoPiaResolutionAssemblies(candidateAssembly);

                    if (resolutionAssemblies.IsDefault)
                        continue;

                    Array.Clear(candidateInputAssemblySymbols, 0, candidateInputAssemblySymbols.Length);
                    var match = true;

                    foreach (var assembly in resolutionAssemblies) {
                        match = false;

                        for (var j = 1; j < totalAssemblies; j++) {
                            if (assemblies[j].IsMatchingAssembly(assembly) &&
                                IsLinked(assembly) == assemblies[j].isLinked) {
                                candidateInputAssemblySymbols[j] = assembly;
                                match = true;
                            }
                        }

                        if (!match)
                            break;
                    }

                    if (!match) {
                        continue;
                    }

                    for (var j = 1; j < totalAssemblies; j++) {
                        if (candidateInputAssemblySymbols[j] is null) {
                            match = false;
                            break;
                        } else {
                            if (corLibraryIndex < 0) {
                                // if (GetCorLibrary(candidateInputAssemblySymbols[j]) != null) {
                                //     // but this assembly has
                                //     // I am leaving the Assert here because it will likely indicate a bug somewhere.
                                //     Debug.Assert(GetCorLibrary(candidateInputAssemblySymbols[j]) == null);
                                //     match = false;
                                //     break;
                                // }
                            } else {
                                Debug.Assert(corLibraryIndex != 0);
                                throw ExceptionUtilities.Unreachable();

                                // if (!ReferenceEquals(candidateInputAssemblySymbols[corLibraryIndex], GetCorLibrary(candidateInputAssemblySymbols[j]))) {
                                //     // I am leaving the Assert here because it will likely indicate a bug somewhere.
                                //     Debug.Assert(candidateInputAssemblySymbols[corLibraryIndex] == null);
                                //     match = false;
                                //     break;
                                // }
                            }
                        }
                    }

                    if (match) {
                        for (var j = 1; j < totalAssemblies; j++) {
                            Debug.Assert(candidateInputAssemblySymbols[j] is not null);
                            boundInputs[j].assemblySymbol = candidateInputAssemblySymbols[j];
                        }

                        return true;
                    }
                }

                Array.Clear(candidateInputAssemblySymbols, 0, candidateInputAssemblySymbols.Length);
                break;
            }

            return false;
        }

        private ImmutableArray<AssemblySymbol> GetNoPiaResolutionAssemblies(AssemblySymbol candidateAssembly) {
            if (candidateAssembly is SourceAssemblySymbol)
                return [];

            return candidateAssembly.GetNoPiaResolutionAssemblies();
        }

        private void ReuseAssemblySymbols(
            BoundInputAssembly[] boundInputs,
            AssemblySymbol[] candidateInputAssemblySymbols,
            ImmutableArray<AssemblyData> assemblies,
            int corLibraryIndex) {
            var candidatesToExamine = CandidatesToExaminePool.Allocate();
            var candidateReferencedSymbols = CandidateReferencedSymbolsPool.Allocate();

            try {
                var totalAssemblies = assemblies.Length;

                for (var i = 1; i < totalAssemblies; i++) {
                    if (boundInputs[i].assemblySymbol is not null || assemblies[i].containsNoPiaLocalTypes)
                        continue;

                    foreach (var candidateAssembly in assemblies[i].availableSymbols) {
                        var match = true;

                        Array.Clear(candidateInputAssemblySymbols, 0, candidateInputAssemblySymbols.Length);
                        candidatesToExamine.Clear();
                        candidatesToExamine.Enqueue(new AssemblyReferenceCandidate(i, candidateAssembly));

                        while (match && candidatesToExamine.Count > 0) {
                            var candidate = candidatesToExamine.Dequeue();

                            Debug.Assert(candidate.definitionIndex >= 0);
                            Debug.Assert(candidate.assemblySymbol is object);

                            var candidateIndex = candidate.definitionIndex;

                            Debug.Assert(boundInputs[candidateIndex].assemblySymbol is null ||
                                candidateInputAssemblySymbols[candidateIndex] is null);

                            var inputAssembly = boundInputs[candidateIndex].assemblySymbol ??
                                candidateInputAssemblySymbols[candidateIndex];

                            if (inputAssembly is not null) {
                                if (ReferenceEquals(inputAssembly, candidate.assemblySymbol))
                                    continue;

                                match = false;
                                break;
                            }

                            if (IsLinked(candidate.assemblySymbol) != assemblies[candidateIndex].isLinked) {
                                match = false;
                                break;
                            }

                            Debug.Assert(candidateInputAssemblySymbols[candidateIndex] is null);
                            candidateInputAssemblySymbols[candidateIndex] = candidate.assemblySymbol;

                            var candidateReferenceBinding = boundInputs[candidateIndex].referenceBinding;

                            candidateReferencedSymbols.Clear();
                            GetActualBoundReferencesUsedBy(candidate.assemblySymbol, candidateReferencedSymbols);

                            Debug.Assert(candidateReferenceBinding is not null);
                            Debug.Assert(candidateReferenceBinding.Length == candidateReferencedSymbols.Count);
                            var referencesCount = candidateReferencedSymbols.Count;

                            for (var k = 0; k < referencesCount; k++) {
                                if (!candidateReferenceBinding[k].isBound) {
                                    if (candidateReferencedSymbols[k] is not null) {
                                        match = false;
                                        break;
                                    }

                                    continue;
                                }

                                var currentCandidateReferencedSymbol = candidateReferencedSymbols[k];

                                if (currentCandidateReferencedSymbol is null) {
                                    match = false;
                                    break;
                                }

                                var definitionIndex = candidateReferenceBinding[k].definitionIndex;

                                if (definitionIndex == 0) {
                                    match = false;
                                    break;
                                }

                                if (!assemblies[definitionIndex].IsMatchingAssembly(currentCandidateReferencedSymbol)) {
                                    match = false;
                                    break;
                                }

                                if (assemblies[definitionIndex].containsNoPiaLocalTypes) {
                                    match = false;
                                    break;
                                }

                                if (IsLinked(currentCandidateReferencedSymbol)
                                    != assemblies[definitionIndex].isLinked) {
                                    match = false;
                                    break;
                                }

                                candidatesToExamine.Enqueue(
                                    new AssemblyReferenceCandidate(definitionIndex, currentCandidateReferencedSymbol)
                                );
                            }

                            if (match) {
                                var candidateCorLibrary = GetCorLibrary(candidate.assemblySymbol);

                                if (candidateCorLibrary is null) {
                                    if (corLibraryIndex >= 0) {
                                        match = false;
                                        break;
                                    }
                                } else {
                                    Debug.Assert(corLibraryIndex != 0);
                                    Debug.Assert(ReferenceEquals(candidateCorLibrary, GetCorLibrary(candidateCorLibrary)));

                                    if (corLibraryIndex < 0) {
                                        match = false;
                                        break;
                                    }

                                    if (!assemblies[corLibraryIndex].IsMatchingAssembly(candidateCorLibrary)) {
                                        match = false;
                                        break;
                                    }

                                    Debug.Assert(!assemblies[corLibraryIndex].containsNoPiaLocalTypes);
                                    Debug.Assert(!assemblies[corLibraryIndex].isLinked);
                                    Debug.Assert(!IsLinked(candidateCorLibrary));

                                    candidatesToExamine.Enqueue(
                                        new AssemblyReferenceCandidate(corLibraryIndex, candidateCorLibrary)
                                    );
                                }
                            }
                        }

                        if (match) {
                            for (var k = 0; k < totalAssemblies; k++) {
                                if (candidateInputAssemblySymbols[k] is not null) {
                                    Debug.Assert(boundInputs[k].assemblySymbol is null);
                                    boundInputs[k].assemblySymbol = candidateInputAssemblySymbols[k];
                                }
                            }

                            break;
                        }
                    }
                }
            } finally {
                candidatesToExamine.Clear();
                candidateReferencedSymbols.Clear();

                CandidatesToExaminePool.Free(candidatesToExamine);
                CandidateReferencedSymbolsPool.Free(candidateReferencedSymbols);
            }
        }

        private AssemblySymbol GetCorLibrary(AssemblySymbol candidateAssembly) {
            var corLibrary = candidateAssembly.corAssembly;
            return corLibrary.isMissing ? null : corLibrary;
        }

        private bool IsLinked(AssemblySymbol candidateAssembly) {
            return candidateAssembly.isLinked;
        }

        private void GetActualBoundReferencesUsedBy(
            AssemblySymbol assemblySymbol,
            List<AssemblySymbol> referencedAssemblySymbols) {
            Debug.Assert(referencedAssemblySymbols.IsEmpty());

            foreach (var module in assemblySymbol.modules)
                referencedAssemblySymbols.AddRange(module.referencedAssemblySymbols);

            for (var i = 0; i < referencedAssemblySymbols.Count; i++) {
                if (referencedAssemblySymbols[i].isMissing)
                    referencedAssemblySymbols[i] = null;
            }
        }

        private ImmutableArray<ResolvedReference> ResolveMetadataReferences(
            Compilation compilation,
            Dictionary<string, List<ReferencedAssemblyIdentity>> assemblyReferencesBySimpleName,
            out ImmutableArray<MetadataReference> references,
            out IDictionary<(string, string), MetadataReference> boundReferenceDirectiveMap,
            out ImmutableArray<MetadataReference> boundReferenceDirectives,
            out ImmutableArray<AssemblyData> assemblies,
            out ImmutableArray<PEModule> modules,
            BelteDiagnosticQueue diagnostics) {
            GetCompilationReferences(
                compilation,
                diagnostics,
                out references,
                out boundReferenceDirectiveMap,
                out var referenceDirectiveLocations
            );

            var referenceCount = references.Length;
            // Purposely using != here
            var referenceDirectiveCount = referenceDirectiveLocations != null
                ? referenceDirectiveLocations.Length
                : 0;

            var referenceMap = new ResolvedReference[referenceCount];

            Dictionary<MetadataReference, MergedAliases> lazyAliasMap = null;

            var boundReferences = new Dictionary<MetadataReference, MetadataReference>(MetadataReferenceEqualityComparer.Instance);

            // Purposely using != here
            var uniqueDirectiveReferences = (referenceDirectiveLocations != null)
                ? ArrayBuilder<MetadataReference>.GetInstance()
                : null;

            var assembliesBuilder = ArrayBuilder<AssemblyData>.GetInstance();
            ArrayBuilder<PEModule>? lazyModulesBuilder = null;

            // TODO
            var supersedeLowerVersions = true;

            for (var referenceIndex = referenceCount - 1; referenceIndex >= 0; referenceIndex--) {
                var boundReference = references[referenceIndex];

                if (boundReference is null)
                    continue;

                if (boundReferences.TryGetValue(boundReference, out var existingReference)) {
                    if ((object)boundReference != existingReference)
                        MergeReferenceProperties(existingReference, boundReference, diagnostics, ref lazyAliasMap);

                    continue;
                }

                boundReferences.Add(boundReference, boundReference);

                TextLocation location;

                if (referenceIndex < referenceDirectiveCount) {
                    location = referenceDirectiveLocations[referenceIndex];
                    uniqueDirectiveReferences.Add(boundReference);
                } else {
                    location = null;
                }

                if (boundReference is CompilationReference compilationReference) {
                    // switch (compilationReference.properties.kind) {
                    //     case MetadataImageKind.Assembly:
                    //         existingReference = TryAddAssembly(
                    //             compilationReference.compilation.assembly.identity,
                    //             boundReference,
                    //             -assembliesBuilder.Count - 1,
                    //             diagnostics,
                    //             location,
                    //             assemblyReferencesBySimpleName,
                    //             supersedeLowerVersions
                    //         );

                    //         if (existingReference is not null) {
                    //             MergeReferenceProperties(
                    //                 existingReference,
                    //                 boundReference,
                    //                 diagnostics,
                    //                 ref lazyAliasMap
                    //             );

                    //             continue;
                    //         }

                    //         var asmData = CreateAssemblyDataForCompilation(compilationReference);
                    //         AddAssembly(asmData, referenceIndex, referenceMap, assembliesBuilder);
                    //         break;
                    //     default:
                    //         throw ExceptionUtilities.UnexpectedValue(compilationReference.properties.kind);
                    // }

                    // continue;
                    throw ExceptionUtilities.Unreachable();
                }

                var peReference = (PortableExecutableReference)boundReference;
                var metadata = GetMetadata(peReference, location, diagnostics);
                Debug.Assert(metadata != null || diagnostics.AnyErrors());

                if (metadata is not null) {
                    switch (peReference.properties.kind) {
                        case MetadataImageKind.Assembly:
                            var assemblyMetadata = (AssemblyMetadata)metadata;
                            var cachedSymbols = assemblyMetadata.cachedSymbols;

                            if (assemblyMetadata.IsValidAssembly()) {
                                var assembly = assemblyMetadata.GetAssembly();
                                Debug.Assert(assembly is not null);

                                existingReference = TryAddAssembly(
                                    assembly.identity,
                                    peReference,
                                    -assembliesBuilder.Count - 1,
                                    diagnostics,
                                    location,
                                    assemblyReferencesBySimpleName,
                                    supersedeLowerVersions
                                );

                                if (existingReference is not null) {
                                    MergeReferenceProperties(
                                        existingReference,
                                        boundReference,
                                        diagnostics,
                                        ref lazyAliasMap
                                    );

                                    continue;
                                }

                                var asmData = CreateAssemblyDataForFile(
                                    assembly,
                                    cachedSymbols,
                                    simpleAssemblyName,
                                    // TODO Should these be changeable
                                    importOptions: MetadataImportOptions.All,
                                    embedInteropTypes: false
                                // compilation.Options.MetadataImportOptions,
                                // peReference.properties.EmbedInteropTypes
                                );

                                AddAssembly(asmData, referenceIndex, referenceMap, assembliesBuilder);
                            } else {
                                // diagnostics.Add(MessageProvider.CreateDiagnostic(MessageProvider.ERR_MetadataFileNotAssembly, location, peReference.Display ?? ""));
                                // TODO If reachable, probably just Error.InvalidReference
                                throw ExceptionUtilities.Unreachable();
                            }

                            GC.KeepAlive(assemblyMetadata);
                            break;
                        case MetadataImageKind.Module:
                            throw ExceptionUtilities.Unreachable();
                        // var moduleMetadata = (ModuleMetadata)metadata;

                        // if (moduleMetadata.module.isLinkedModule) {
                        //     if (!moduleMetadata.module.isEntireImageAvailable) {
                        //         diagnostics.Add(MessageProvider.CreateDiagnostic(MessageProvider.ERR_LinkedNetmoduleMetadataMustProvideFullPEImage, location, peReference.Display ?? ""));
                        //     }

                        //     AddModule(moduleMetadata.Module, referenceIndex, referenceMap, ref lazyModulesBuilder);
                        // } else {
                        //     diagnostics.Add(MessageProvider.CreateDiagnostic(MessageProvider.ERR_MetadataFileNotModule, location, peReference.Display ?? ""));
                        // }
                        // break;
                        default:
                            throw ExceptionUtilities.UnexpectedValue(peReference.properties.kind);
                    }
                }
            }

            if (uniqueDirectiveReferences is not null) {
                uniqueDirectiveReferences.ReverseContents();
                boundReferenceDirectives = uniqueDirectiveReferences.ToImmutableAndFree();
            } else {
                boundReferenceDirectives = [];
            }

            for (var i = 0; i < referenceMap.Length; i++) {
                if (!referenceMap[i].isSkipped) {
                    var count = (referenceMap[i].kind == MetadataImageKind.Assembly)
                        ? assembliesBuilder.Count
                        : lazyModulesBuilder?.Count ?? 0;

                    var reversedIndex = count - 1 - referenceMap[i].index;

                    referenceMap[i] = GetResolvedReferenceAndFreePropertyMapEntry(
                        references[i],
                        reversedIndex,
                        referenceMap[i].kind,
                        lazyAliasMap
                    );
                }
            }

            assembliesBuilder.ReverseContents();
            assemblies = assembliesBuilder.ToImmutableAndFree();

            if (lazyModulesBuilder == null) {
                modules = ImmutableArray<PEModule>.Empty;
            } else {
                lazyModulesBuilder.ReverseContents();
                modules = lazyModulesBuilder.ToImmutableAndFree();
            }

            return ImmutableArray.CreateRange(referenceMap);
        }

        private static void AddAssembly(
            AssemblyData data,
            int referenceIndex,
            ResolvedReference[] referenceMap,
            ArrayBuilder<AssemblyData> assemblies) {
            referenceMap[referenceIndex] = new ResolvedReference(assemblies.Count, MetadataImageKind.Assembly);
            assemblies.Add(data);
        }

        private MetadataReference TryAddAssembly(
            AssemblyIdentity identity,
            MetadataReference reference,
            int assemblyIndex,
            BelteDiagnosticQueue diagnostics,
            TextLocation location,
            Dictionary<string, List<ReferencedAssemblyIdentity>> referencesBySimpleName,
            bool supersedeLowerVersions) {
            var referencedAssembly = new ReferencedAssemblyIdentity(identity, reference, assemblyIndex);

            if (!referencesBySimpleName.TryGetValue(identity.name, out var sameSimpleNameIdentities)) {
                referencesBySimpleName.Add(identity.name, new List<ReferencedAssemblyIdentity> { referencedAssembly });
                return null;
            }

            if (supersedeLowerVersions) {
                foreach (var other in sameSimpleNameIdentities) {
                    Debug.Assert(other.identity is not null);

                    if (identity.version == other.identity.version)
                        return other.reference;
                }

                if (sameSimpleNameIdentities[0].identity.version > identity.version) {
                    sameSimpleNameIdentities.Add(referencedAssembly);
                } else {
                    sameSimpleNameIdentities.Add(sameSimpleNameIdentities[0]);
                    sameSimpleNameIdentities[0] = referencedAssembly;
                }

                return null;
            }

            ReferencedAssemblyIdentity equivalent = default;

            if (identity.isStrongName) {
                foreach (var other in sameSimpleNameIdentities) {
                    Debug.Assert(other.identity is not null);

                    if (other.identity.isStrongName &&
                        identityComparer.ReferenceMatchesDefinition(identity, other.identity) &&
                        identityComparer.ReferenceMatchesDefinition(other.identity, identity)) {
                        equivalent = other;
                        break;
                    }
                }
            } else {
                foreach (var other in sameSimpleNameIdentities) {
                    Debug.Assert(other.identity is not null);

                    if (!other.identity.isStrongName && WeakIdentityPropertiesEquivalent(identity, other.identity)) {
                        equivalent = other;
                        break;
                    }
                }
            }

            if (equivalent.identity is null) {
                sameSimpleNameIdentities.Add(referencedAssembly);
                return null;
            }

            if (identity.isStrongName) {
                Debug.Assert(equivalent.identity.isStrongName);

                if (identity != equivalent.identity) {
                    // MessageProvider.ReportDuplicateMetadataReferenceStrong(diagnostics, location, reference, identity, equivalent.Reference!, equivalent.Identity);
                    throw ExceptionUtilities.Unreachable();
                }
            } else {
                Debug.Assert(!equivalent.identity.isStrongName);

                if (identity != equivalent.identity) {
                    // MessageProvider.ReportDuplicateMetadataReferenceWeak(diagnostics, location, reference, identity, equivalent.Reference!, equivalent.Identity);
                    throw ExceptionUtilities.Unreachable();
                }
            }

            Debug.Assert(equivalent.reference is not null);
            return equivalent.reference;
        }

        private bool WeakIdentityPropertiesEquivalent(AssemblyIdentity identity1, AssemblyIdentity identity2) {
            Debug.Assert(AssemblyIdentityComparer.SimpleNameComparer.Equals(identity1.name, identity2.name));
            return AssemblyIdentityComparer.CultureComparer.Equals(identity1.cultureName, identity2.cultureName);
        }

        private Metadata GetMetadata(
            PortableExecutableReference peReference,
            TextLocation location,
            BelteDiagnosticQueue diagnostics) {
            Metadata existingMetadata;

            lock (observedMetadata) {
                if (TryGetObservedMetadata(peReference, diagnostics, out existingMetadata))
                    return existingMetadata;
            }

            Metadata newMetadata;
            BelteDiagnostic newDiagnostic = null;

            try {
                newMetadata = peReference.GetMetadataNoCopy();

                if (newMetadata is AssemblyMetadata assemblyMetadata) {
                    _ = assemblyMetadata.IsValidAssembly();
                } else {
                    _ = ((ModuleMetadata)newMetadata).module.isLinkedModule;
                }
            } catch (Exception e) when (e is BadImageFormatException || e is IOException) {
                // newDiagnostic = PortableExecutableReference.ExceptionToDiagnostic(e, messageProvider, location, peReference.Display ?? "", peReference.Properties.Kind);
                // newMetadata = null;
                throw ExceptionUtilities.Unreachable();
            }

            lock (observedMetadata) {
                if (TryGetObservedMetadata(peReference, diagnostics, out existingMetadata))
                    return existingMetadata;

                if (newDiagnostic is not null)
                    diagnostics.Push(newDiagnostic);

                observedMetadata.Add(peReference, (object)newMetadata ?? newDiagnostic);
                return newMetadata;
            }
        }

        private bool TryGetObservedMetadata(
            PortableExecutableReference peReference,
            BelteDiagnosticQueue diagnostics,
            out Metadata metadata) {
            if (observedMetadata.TryGetValue(peReference, out var existing)) {
                Debug.Assert(existing is Metadata || existing is BelteDiagnostic);

                metadata = existing as Metadata;

                if (metadata is null)
                    diagnostics.Push((BelteDiagnostic)existing);

                return true;
            }

            metadata = null;
            return false;
        }

        private AssemblyData CreateAssemblyDataForFile(
            PEAssembly assembly,
            WeakList<AssemblySymbol> cachedSymbols,
            string sourceAssemblySimpleName,
            MetadataImportOptions importOptions,
            bool embedInteropTypes) {
            return new AssemblyDataForFile(
                assembly,
                cachedSymbols,
                embedInteropTypes,
                sourceAssemblySimpleName,
                importOptions
            );
        }

        internal static AssemblyReferenceBinding[] ResolveReferencedAssemblies(
            ImmutableArray<AssemblyIdentity> references,
            MultiDictionary<string, (AssemblyData DefinitionData, int DefinitionIndex)> definitions,
            bool resolveAgainstAssemblyBeingBuilt,
            AssemblyIdentityComparer assemblyIdentityComparer) {
            var boundReferences = new AssemblyReferenceBinding[references.Length];
            for (var j = 0; j < references.Length; j++) {
                boundReferences[j] = ResolveReferencedAssembly(
                    references[j],
                    definitions,
                    resolveAgainstAssemblyBeingBuilt,
                    assemblyIdentityComparer
                );
            }

            return boundReferences;
        }

        internal static AssemblyReferenceBinding ResolveReferencedAssembly(
            AssemblyIdentity reference,
            MultiDictionary<string, (AssemblyData DefinitionData, int DefinitionIndex)> definitions,
            bool resolveAgainstAssemblyBeingBuilt,
            AssemblyIdentityComparer assemblyIdentityComparer) {
            var minHigherVersionDefinition = -1;
            Version minHigherVersionDefinitionVersion = null;
            var maxLowerVersionDefinition = -1;
            Version maxLowerVersionDefinitionVersion = null;

            foreach ((var definitionData, var definitionIndex) in definitions[reference.name]) {
                if (definitionIndex == 0)
                    continue;

                var definition = definitionData.identity;

                switch (assemblyIdentityComparer.Compare(reference, definition)) {
                    case AssemblyIdentityComparer.ComparisonResult.NotEquivalent:
                        continue;
                    case AssemblyIdentityComparer.ComparisonResult.Equivalent:
                        return new AssemblyReferenceBinding(reference, definitionIndex);
                    case AssemblyIdentityComparer.ComparisonResult.EquivalentIgnoringVersion:
                        if (reference.version < definition.version) {
                            if (minHigherVersionDefinition == -1 ||
                                definition.version < minHigherVersionDefinitionVersion) {
                                minHigherVersionDefinition = definitionIndex;
                                minHigherVersionDefinitionVersion = definition.version;
                            }
                        } else {
                            Debug.Assert(reference.version > definition.version);

                            if (maxLowerVersionDefinition == -1 ||
                                definition.version > maxLowerVersionDefinitionVersion) {
                                maxLowerVersionDefinition = definitionIndex;
                                maxLowerVersionDefinitionVersion = definition.version;
                            }
                        }

                        continue;
                    default:
                        throw ExceptionUtilities.Unreachable();
                }
            }

            if (minHigherVersionDefinition != -1)
                return new AssemblyReferenceBinding(reference, minHigherVersionDefinition, versionDifference: +1);

            if (maxLowerVersionDefinition != -1)
                return new AssemblyReferenceBinding(reference, maxLowerVersionDefinition, versionDifference: -1);

            // TODO What is this
            // if (reference.IsWindowsComponent()) {
            //     foreach ((var definitionData, var definitionIndex) in definitions[AssemblyIdentityExtensions.WindowsRuntimeIdentitySimpleName]) {
            //         // Skip assembly being built for now; it will be considered at the very end
            //         if (definitionIndex == 0) {
            //             continue;
            //         }

            //         if (definitionData.Identity.IsWindowsRuntime()) {
            //             return new AssemblyReferenceBinding(reference, definitionIndex);
            //         }
            //     }
            // }

            // if (reference.contentType == AssemblyContentType.WindowsRuntime) {
            //     foreach ((var definitionData, var definitionIndex) in definitions[reference.name]) {
            //         if (definitionIndex == 0)
            //             continue;

            //         var definition = definitionData.identity;
            //         var sourceCompilation = definitionData.sourceCompilation;

            //         if (definition.contentType == AssemblyContentType.Default &&
            //             sourceCompilation?.Options.OutputKind == OutputKind.WindowsRuntimeMetadata &&
            //             reference.Version.Equals(definition.Version) &&
            //             reference.IsRetargetable == definition.IsRetargetable &&
            //             AssemblyIdentityComparer.CultureComparer.Equals(reference.CultureName, definition.CultureName) &&
            //             AssemblyIdentity.KeysEqual(reference, definition)) {
            //             return new AssemblyReferenceBinding(reference, definitionIndex);
            //         }
            //     }
            // }

            if (resolveAgainstAssemblyBeingBuilt) {
                foreach ((var definitionData, var definitionIndex) in definitions[reference.name]) {
                    if (definitionIndex == 0) {
                        Debug.Assert(definitionData.identity.publicKeyToken.IsEmpty);
                        return new AssemblyReferenceBinding(reference, 0);
                    }
                }
            }

            return new AssemblyReferenceBinding(reference);
        }

        private static ResolvedReference GetResolvedReferenceAndFreePropertyMapEntry(
            MetadataReference reference,
            int index,
            MetadataImageKind kind,
            Dictionary<MetadataReference, MergedAliases> propertyMapOpt) {
            ImmutableArray<string> aliasesOpt, recursiveAliasesOpt;
            var mergedReferences = ImmutableArray<MetadataReference>.Empty;

            if (propertyMapOpt is not null && propertyMapOpt.TryGetValue(reference, out var mergedProperties)) {
                aliasesOpt = mergedProperties.aliasesOpt?.ToImmutableAndFree() ?? default;
                recursiveAliasesOpt = mergedProperties.recursiveAliasesOpt?.ToImmutableAndFree() ?? default;

                if (mergedProperties.mergedReferencesOpt is not null)
                    mergedReferences = mergedProperties.mergedReferencesOpt.ToImmutableAndFree();
            } else if (reference.properties.hasRecursiveAliases) {
                aliasesOpt = default;
                recursiveAliasesOpt = reference.properties.aliases;
            } else {
                aliasesOpt = reference.properties.aliases;
                recursiveAliasesOpt = default;
            }

            return new ResolvedReference(index, kind, aliasesOpt, recursiveAliasesOpt, mergedReferences);
        }

        private void GetCompilationReferences(
            Compilation compilation,
            BelteDiagnosticQueue diagnostics,
            out ImmutableArray<MetadataReference> references,
            out IDictionary<(string, string), MetadataReference> boundReferenceDirectives,
            out ImmutableArray<TextLocation> referenceDirectiveLocations) {
            var referencesBuilder = ArrayBuilder<MetadataReference>.GetInstance();
            ArrayBuilder<TextLocation> referenceDirectiveLocationsBuilder = null;
            IDictionary<(string, string), MetadataReference> localBoundReferenceDirectives = null;

            try {
                referencesBuilder.AddRange(compilation.externalReferences);
                localBoundReferenceDirectives ??= SpecializedCollections.EmptyDictionary<(string, string), MetadataReference>();
                boundReferenceDirectives = localBoundReferenceDirectives;
                references = referencesBuilder.ToImmutable();
                referenceDirectiveLocations = referenceDirectiveLocationsBuilder?.ToImmutableAndFree() ?? [];
            } finally {
                referencesBuilder.Free();
            }
        }

        private void MergeReferenceProperties(
            MetadataReference primaryReference,
            MetadataReference newReference,
            BelteDiagnosticQueue diagnostics,
            ref Dictionary<MetadataReference, MergedAliases> lazyAliasMap) {
            if (!CheckPropertiesConsistency(newReference, primaryReference, diagnostics))
                return;

            lazyAliasMap ??= [];

            if (!lazyAliasMap.TryGetValue(primaryReference, out var mergedAliases)) {
                mergedAliases = new MergedAliases();
                lazyAliasMap.Add(primaryReference, mergedAliases);
                mergedAliases.Merge(primaryReference);
            }

            mergedAliases.Merge(newReference);
        }

        private bool CheckPropertiesConsistency(
            MetadataReference primaryReference,
            MetadataReference duplicateReference,
            BelteDiagnosticQueue diagnostics) {
            if (primaryReference.properties.embedInteropTypes != duplicateReference.properties.embedInteropTypes) {
                // diagnostics.Add(ErrorCode.ERR_AssemblySpecifiedForLinkAndRef, NoLocation.Singleton, duplicateReference.Display, primaryReference.Display);
                throw ExceptionUtilities.Unreachable();
                // return false;
            }

            return true;
        }
    }
}
