using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Buckle.CodeAnalysis.Display;
using Buckle.CodeAnalysis.Text;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class PEAssemblySymbol : MetadataOrSourceAssemblySymbol {
    private readonly PEAssembly _assembly;
    private readonly ImmutableArray<ModuleSymbol> _modules;
    private ImmutableArray<AssemblySymbol> _noPiaResolutionAssemblies;
    private ImmutableArray<AssemblySymbol> _linkedReferencedAssemblies;
    private readonly bool _isLinked;
    private int _lazyBelteMetadataVersion = -1;
    private bool _lazyIsBelteAssembly;

    private ImmutableArray<AttributeData> _lazyCustomAttributes;

    internal PEAssemblySymbol(PEAssembly assembly, bool isLinked, MetadataImportOptions importOptions) {
        _assembly = assembly;
        var modules = new ModuleSymbol[assembly.modules.Length];

        for (var i = 0; i < assembly.modules.Length; i++)
            modules[i] = new PEModuleSymbol(this, assembly.modules[i], importOptions, i);

        _modules = modules.AsImmutableOrNull();
        _isLinked = isLinked;
    }

    internal PEAssembly assembly => _assembly;

    internal override AssemblyIdentity identity => _assembly.identity;

    internal override ImmutableArray<ModuleSymbol> modules => _modules;

    internal override ImmutableArray<TextLocation> locations
        => primaryModule.metadataLocation.Cast<MetadataLocation, TextLocation>();

    internal override bool isBelteAssembly {
        get {
            EnsureBelteMetadataAttributeIsRead();
            return _lazyIsBelteAssembly;
        }
    }

    internal override int belteMetadataVersion {
        get {
            EnsureBelteMetadataAttributeIsRead();

            if (!isBelteAssembly)
                throw ExceptionUtilities.Unreachable();

            return _lazyBelteMetadataVersion;
        }
    }

    internal (AssemblySymbol FirstSymbol, AssemblySymbol SecondSymbol) LookupAssembliesForForwardedMetadataType(
        ref MetadataTypeName emittedName) {
        return primaryModule.GetAssembliesForForwardedType(ref emittedName);
    }

    internal override ImmutableArray<AttributeData> GetAttributes() {
        if (_lazyCustomAttributes.IsDefault)
            ImmutableInterlocked.InterlockedInitialize(ref _lazyCustomAttributes, LoadAndFilterAttributes());

        return _lazyCustomAttributes;

        ImmutableArray<AttributeData> LoadAndFilterAttributes() {
            var containingModule = primaryModule;

            if (!containingModule.TryGetNonEmptyCustomAttributes(_assembly.handle, out var customAttributeHandles))
                return [];

            // TODO
            // var mightContainExtensions = this.mightContainExtensions;
            var mightContainExtensions = true;

            using var builder = TemporaryArray<AttributeData>.Empty;

            foreach (var handle in customAttributeHandles) {
                if (mightContainExtensions && containingModule.AttributeMatchesFilter(
                        handle,
                        AttributeDescription.CaseSensitiveExtensionAttribute)) {
                    continue;
                }

                builder.Add(new PEAttributeData(containingModule, handle));
            }

            return builder.ToImmutableAndClear();
        }
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

    private void EnsureBelteMetadataAttributeIsRead() {
        if (_lazyBelteMetadataVersion == -1) {
            var metadataVersion = ReadBelteMetadataVersion();

            if (!metadataVersion.HasValue) {
                Interlocked.CompareExchange(ref _lazyBelteMetadataVersion, -2, -1);
                Interlocked.Exchange(ref _lazyIsBelteAssembly, false);
            } else {
                Interlocked.CompareExchange(ref _lazyBelteMetadataVersion, metadataVersion.Value, -1);
                Interlocked.Exchange(ref _lazyIsBelteAssembly, true);
            }
        }

        Debug.Assert(_lazyBelteMetadataVersion != -1);
    }

    private int? ReadBelteMetadataVersion() {
        foreach (var attribute in GetAttributes()) {
            // TODO I'd rather this checks the attribute is from the Belte.Core assembly than doing a name search
            if (attribute.attributeClass?.metadataName != "BelteMetadataAttribute")
                continue;

            if (attribute.attributeClass.containingNamespace
                .ToDisplayString(SymbolDisplayFormat.NamespaceQualifiedNameFormat) != "Belte.CompilerServices") {
                continue;
            }

            if (attribute._commonConstructorArguments.Length != 1)
                continue;

            return (int)attribute._commonConstructorArguments[0].value;
        }

        return null;
    }
}
