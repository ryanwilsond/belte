using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;
using EntityHandle = System.Reflection.Metadata.EntityHandle;

namespace Buckle.CodeAnalysis;

internal sealed class PEAssembly {
    internal readonly ImmutableArray<AssemblyIdentity> assemblyReferences;

    internal readonly ImmutableArray<int> moduleReferenceCounts;

    private readonly ImmutableArray<PEModule> _modules;

    private readonly AssemblyIdentity _identity;

    private ThreeState _lazyContainsNoPiaLocalTypes;

    private ThreeState _lazyDeclaresTheObjectClass;

    private readonly AssemblyMetadata _owner;

    private Dictionary<string, List<ImmutableArray<byte>>> _lazyInternalsVisibleToMap;

    internal PEAssembly(AssemblyMetadata owner, ImmutableArray<PEModule> modules) {
        _identity = modules[0].ReadAssemblyIdentityOrThrow();

        var totalRefCount = modules.Sum(static module => module.referencedAssemblies.Length);
        var refCounts = ArrayBuilder<int>.GetInstance(modules.Length);
        var refs = ArrayBuilder<AssemblyIdentity>.GetInstance(totalRefCount);

        for (var i = 0; i < modules.Length; i++) {
            var refsForModule = modules[i].referencedAssemblies;
            refCounts.Add(refsForModule.Length);
            refs.AddRange(refsForModule);
        }

        _modules = modules;
        assemblyReferences = refs.ToImmutableAndFree();
        moduleReferenceCounts = refCounts.ToImmutableAndFree();
        _owner = owner;
    }

    internal EntityHandle handle => EntityHandle.AssemblyDefinition;

    internal PEModule manifestModule => modules[0];

    internal ImmutableArray<PEModule> modules => _modules;

    internal AssemblyIdentity identity => _identity;

    internal string location => _owner.location;

    internal bool ContainsNoPiaLocalTypes() {
        if (_lazyContainsNoPiaLocalTypes == ThreeState.Unknown) {
            foreach (var module in modules) {
                if (module.ContainsNoPiaLocalTypes()) {
                    _lazyContainsNoPiaLocalTypes = ThreeState.True;
                    return true;
                }
            }

            _lazyContainsNoPiaLocalTypes = ThreeState.False;
        }

        return _lazyContainsNoPiaLocalTypes == ThreeState.True;
    }

    private Dictionary<string, List<ImmutableArray<byte>>> BuildInternalsVisibleToMap() {
        var ivtMap = new Dictionary<string, List<ImmutableArray<byte>>>(StringComparer.OrdinalIgnoreCase);

        foreach (var attrVal in modules[0].GetInternalsVisibleToAttributeValues(handle)) {
            if (AssemblyIdentity.TryParseDisplayName(attrVal, out var identity)) {
                if (ivtMap.TryGetValue(identity.name, out var keys)) {
                    keys.Add(identity.publicKey);
                } else {
                    keys = [identity.publicKey];
                    ivtMap[identity.name] = keys;
                }
            }
        }

        return ivtMap;
    }

    internal IEnumerable<ImmutableArray<byte>> GetInternalsVisibleToPublicKeys(string simpleName) {
        EnsureInternalsVisibleToMapInitialized();

        _lazyInternalsVisibleToMap.TryGetValue(simpleName, out var result);

        return result ?? SpecializedCollections.EmptyEnumerable<ImmutableArray<byte>>();
    }

    internal IEnumerable<string> GetInternalsVisibleToAssemblyNames() {
        EnsureInternalsVisibleToMapInitialized();

        return _lazyInternalsVisibleToMap.Keys;
    }

    private void EnsureInternalsVisibleToMapInitialized() {
        if (_lazyInternalsVisibleToMap is null)
            Interlocked.CompareExchange(ref _lazyInternalsVisibleToMap, BuildInternalsVisibleToMap(), null);
    }

    internal bool declaresTheObjectClass {
        get {
            if (_lazyDeclaresTheObjectClass == ThreeState.Unknown) {
                var value = _modules[0].metadataReader.DeclaresTheObjectClass();
                _lazyDeclaresTheObjectClass = value.ToThreeState();
            }

            return _lazyDeclaresTheObjectClass == ThreeState.True;
        }
    }

    public AssemblyMetadata GetNonDisposableMetadata() => _owner.Copy();
}
