using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class PEAssemblySymbol : MetadataOrSourceAssemblySymbol {
    private readonly PEAssembly _assembly;
    private readonly ImmutableArray<ModuleSymbol> _modules;
    private ImmutableArray<AssemblySymbol> _noPiaResolutionAssemblies;
    private ImmutableArray<AssemblySymbol> _linkedReferencedAssemblies;
    private readonly bool _isLinked;
    private ImmutableArray<AttributeData> _lazyCustomAttributes;

    internal PEAssemblySymbol(PEAssembly assembly, bool isLinked, MetadataImportOptions importOptions) {
        _assembly = assembly;
        var modules = new ModuleSymbol[assembly.modules.Length];

        for (var i = 0; i < assembly.modules.Length; i++) {
            modules[i] = new PEModuleSymbol(this, assembly.modules[i], importOptions, i);
        }

        _modules = modules.AsImmutableOrNull();
        _isLinked = isLinked;
    }

    internal PEAssembly assembly => _assembly;

    internal override AssemblyIdentity identity => _assembly.identity;

    internal override ImmutableArray<ModuleSymbol> modules => _modules;

    internal override ImmutableArray<TextLocation> locations
        => primaryModule.metadataLocation.Cast<MetadataLocation, TextLocation>();

    // internal override int metadataToken => MetadataTokens.GetToken(_assembly.handle);

    // internal override bool hasImportedFromTypeLibAttribute
    //     => primaryModule.module.HasImportedFromTypeLibAttribute(assembly.handle, out _);

    // internal override bool hasPrimaryInteropAssemblyAttribute
    //     => primaryModule.module.HasPrimaryInteropAssemblyAttribute(assembly.handle, out _, out _);

    // internal override ImmutableArray<AttributeData> GetAttributes() {
    //     if (_lazyCustomAttributes.IsDefault)
    //         primaryModule.LoadCustomAttributes(_assembly.handle, ref _lazyCustomAttributes);

    //     return _lazyCustomAttributes;
    // }

    internal (AssemblySymbol FirstSymbol, AssemblySymbol SecondSymbol) LookupAssembliesForForwardedMetadataType(
        ref MetadataTypeName emittedName) {
        return primaryModule.GetAssembliesForForwardedType(ref emittedName);
    }

    internal override IEnumerable<NamedTypeSymbol> GetAllTopLevelForwardedTypes() {
        return primaryModule.GetForwardedTypes();
    }

    internal override NamedTypeSymbol TryLookupForwardedMetadataTypeWithCycleDetection(
        ref MetadataTypeName emittedName,
        ConsList<AssemblySymbol> visitedAssemblies) {
        var (firstSymbol, secondSymbol) = LookupAssembliesForForwardedMetadataType(ref emittedName);

        if (firstSymbol is not null) {
            if (secondSymbol is not null) {
                return CreateMultipleForwardingErrorTypeSymbol(
                    ref emittedName,
                    primaryModule,
                    firstSymbol,
                    secondSymbol
                );
            }

            if (visitedAssemblies is not null && visitedAssemblies.Contains(firstSymbol)) {
                return CreateCycleInTypeForwarderErrorTypeSymbol(ref emittedName);
            } else {
                visitedAssemblies = new ConsList<AssemblySymbol>(
                    this,
                    visitedAssemblies ?? ConsList<AssemblySymbol>.Empty
                );

                return firstSymbol.LookupDeclaredOrForwardedTopLevelMetadataType(ref emittedName, visitedAssemblies);
            }
        }

        return null;
    }

    internal override ImmutableArray<AssemblySymbol> GetNoPiaResolutionAssemblies() {
        return _noPiaResolutionAssemblies;
    }

    internal override void SetNoPiaResolutionAssemblies(ImmutableArray<AssemblySymbol> assemblies) {
        _noPiaResolutionAssemblies = assemblies;
    }

    internal override void SetLinkedReferencedAssemblies(ImmutableArray<AssemblySymbol> assemblies) {
        _linkedReferencedAssemblies = assemblies;
    }

    internal override ImmutableArray<AssemblySymbol> GetLinkedReferencedAssemblies() {
        return _linkedReferencedAssemblies;
    }

    internal override ImmutableArray<byte> publicKey => identity.publicKey;

    internal override bool GetGuidString(out string guidString) {
        return assembly.modules[0].HasGuidAttribute(assembly.handle, out guidString);
    }

    internal override bool AreInternalsVisibleToThisAssembly(AssemblySymbol potentialGiverOfAccess) {
        var conclusion = MakeFinalIVTDetermination(potentialGiverOfAccess);
        return conclusion == IVTConclusion.Match || conclusion == IVTConclusion.OneSignedOneNot;
    }

    internal override IEnumerable<ImmutableArray<byte>> GetInternalsVisibleToPublicKeys(string simpleName) {
        return assembly.GetInternalsVisibleToPublicKeys(simpleName);
    }

    internal override IEnumerable<string> GetInternalsVisibleToAssemblyNames() {
        return assembly.GetInternalsVisibleToAssemblyNames();
    }

    internal override bool isLinked => _isLinked;

    internal PEModuleSymbol primaryModule => (PEModuleSymbol)_modules[0];

    internal sealed override Compilation declaringCompilation => null;

    internal override AssemblyMetadata GetMetadata() => _assembly.GetNonDisposableMetadata();
}
