using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class MetadataOrSourceAssemblySymbol : NonMissingAssemblySymbol {
    private ICollection<string> _lazyTypeNames;
    private ICollection<string> _lazyNamespaceNames;
    private ConcurrentDictionary<AssemblySymbol, IVTConclusion> _lazyAssembliesToWhichInternalAccessHasBeenAnalyzed;

    internal override ICollection<string> typeNames {
        get {
            if (_lazyTypeNames is null)
                Interlocked.CompareExchange(ref _lazyTypeNames, declaringCompilation.declarationTable.typeNames, null);

            return _lazyTypeNames;
        }
    }

    internal override ICollection<string> namespaceNames {
        get {
            if (_lazyNamespaceNames == null) {
                Interlocked.CompareExchange(
                    ref _lazyNamespaceNames,
                    declaringCompilation.declarationTable.namespaceNames,
                    null
                );
            }

            return _lazyNamespaceNames;
        }
    }

    private ConcurrentDictionary<AssemblySymbol, IVTConclusion> _assembliesToWhichInternalAccessHasBeenDetermined {
        get {
            if (_lazyAssembliesToWhichInternalAccessHasBeenAnalyzed is null) {
                Interlocked.CompareExchange(
                    ref _lazyAssembliesToWhichInternalAccessHasBeenAnalyzed,
                    new ConcurrentDictionary<AssemblySymbol, IVTConclusion>(),
                    null
                );
            }

            return _lazyAssembliesToWhichInternalAccessHasBeenAnalyzed;
        }
    }

    private protected IVTConclusion MakeFinalIVTDetermination(AssemblySymbol potentialGiverOfAccess) {
        if (_assembliesToWhichInternalAccessHasBeenDetermined.TryGetValue(potentialGiverOfAccess, out var result))
            return result;

        result = IVTConclusion.NoRelationshipClaimed;
        var publicKeys = potentialGiverOfAccess.GetInternalsVisibleToPublicKeys(name);

        if (publicKeys.Any() && IsNetModule())
            return IVTConclusion.Match;

        foreach (var key in publicKeys) {
            result = potentialGiverOfAccess.identity.PerformIVTCheck(publicKey, key);

            if (result == IVTConclusion.Match || result == IVTConclusion.OneSignedOneNot)
                break;
        }

        _assembliesToWhichInternalAccessHasBeenDetermined.TryAdd(potentialGiverOfAccess, result);
        return result;
    }

    internal virtual bool IsNetModule() => false;
}
