using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Buckle.CodeAnalysis.Binding;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class MetadataOrSourceAssemblySymbol : NonMissingAssemblySymbol {
    private ICollection<string> _lazyTypeNames;
    private ICollection<string> _lazyNamespaceNames;
    private NamedTypeSymbol[] _lazySpecialTypes;
    private TypeConversions _lazyTypeConversions;
    private int _cachedSpecialTypes;
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

    internal sealed override TypeConversions typeConversions {
        get {
            if (this != corAssemblies[0])
                return corAssemblies[0].typeConversions;

            if (_lazyTypeConversions is null)
                Interlocked.CompareExchange(ref _lazyTypeConversions, new TypeConversions(corLibrary), null);

            return _lazyTypeConversions;
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

    internal override bool keepLookingForDeclaredSpecialTypes {
        get {
            if (ReferenceEquals(corAssemblies[0], this))
                return _cachedSpecialTypes < (int)SpecialType.NextAvailable - 1;
            else
                return true;
        }
    }

    internal sealed override NamedTypeSymbol GetDeclaredSpecialType(SpecialType type, bool netMode) {
        if (_lazySpecialTypes is null || _lazySpecialTypes[(int)type] is null) {
            var emittedName = MetadataTypeName.FromFullName(
                type.GetMetadataName(netMode),
                useCLSCompliantNameArityEncoding: true
            );

            var module = modules[0];
            var result = module.LookupTopLevelMetadataType(ref emittedName);

            Debug.Assert(result?.IsErrorType() != true);

            if (result is null || result.declaredAccessibility != Accessibility.Public)
                result = new MissingMetadataTypeSymbol.TopLevel(module, ref emittedName, type);

            RegisterDeclaredSpecialType(result);
        }

        Debug.Assert(_lazySpecialTypes is not null);
        return _lazySpecialTypes[(int)type];
    }

    internal sealed override void RegisterDeclaredSpecialType(NamedTypeSymbol corType) {
        var typeId = corType.specialType;
        Debug.Assert(typeId != SpecialType.None);
        Debug.Assert(ReferenceEquals(corType.containingAssembly, this));
        Debug.Assert(corType.containingModule.ordinal == 0);

        if (_lazySpecialTypes is null) {
            Interlocked.CompareExchange(ref _lazySpecialTypes,
                new NamedTypeSymbol[(int)SpecialType.NextAvailable],
                null
            );
        }

        if (Interlocked.CompareExchange(ref _lazySpecialTypes[(int)typeId], corType, null) is not null) {
            Debug.Assert(ReferenceEquals(corType, _lazySpecialTypes[(int)typeId]) ||
                (corType.kind == SymbolKind.ErrorType &&
                _lazySpecialTypes[(int)typeId].kind == SymbolKind.ErrorType));
        } else {
            Interlocked.Increment(ref _cachedSpecialTypes);
            Debug.Assert(_cachedSpecialTypes > 0 && _cachedSpecialTypes < (int)SpecialType.NextAvailable);
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
