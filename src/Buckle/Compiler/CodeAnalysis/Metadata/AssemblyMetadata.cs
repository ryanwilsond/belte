using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Threading;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis;

internal sealed partial class AssemblyMetadata : Metadata {
    private readonly Func<string, ModuleMetadata> _moduleFactoryOpt;
    private readonly ImmutableArray<ModuleMetadata> _initialModules;
    private Data _lazyData;
    private ImmutableArray<ModuleMetadata> _lazyPublishedModules;

    internal readonly WeakList<AssemblySymbol> cachedSymbols = new WeakList<AssemblySymbol>();

    private AssemblyMetadata(AssemblyMetadata other, bool shareCachedSymbols)
        : base(isImageOwner: false, id: other.id) {
        if (shareCachedSymbols)
            cachedSymbols = other.cachedSymbols;

        _lazyData = other._lazyData;
        _moduleFactoryOpt = other._moduleFactoryOpt;
        _initialModules = other._initialModules;
    }

    internal AssemblyMetadata(ImmutableArray<ModuleMetadata> modules)
        : base(isImageOwner: true, id: MetadataId.CreateNewId()) {
        _initialModules = modules;
    }

    internal AssemblyMetadata(ModuleMetadata manifestModule, Func<string, ModuleMetadata> moduleFactory, string path)
        : base(isImageOwner: true, id: MetadataId.CreateNewId()) {
        _initialModules = [manifestModule];
        _moduleFactoryOpt = moduleFactory;
        location = path;
    }

    internal string location { get; }

    internal static AssemblyMetadata CreateFromImage(ImmutableArray<byte> peImage) {
        return Create(ModuleMetadata.CreateFromImage(peImage));
    }

    internal static AssemblyMetadata CreateFromImage(IEnumerable<byte> peImage) {
        return Create(ModuleMetadata.CreateFromImage(peImage));
    }

    internal static AssemblyMetadata CreateFromStream(Stream peStream, bool leaveOpen = false) {
        return Create(ModuleMetadata.CreateFromStream(peStream, leaveOpen));
    }

    internal static AssemblyMetadata CreateFromStream(Stream peStream, PEStreamOptions options) {
        return Create(ModuleMetadata.CreateFromStream(peStream, options));
    }

    internal static AssemblyMetadata CreateFromFile(string path) {
        return CreateFromFile(ModuleMetadata.CreateFromFile(path), path);
    }

    internal static AssemblyMetadata CreateFromFile(ModuleMetadata manifestModule, string path) {
        return new AssemblyMetadata(
            manifestModule,
            moduleName => ModuleMetadata.CreateFromFile(Path.Combine(Path.GetDirectoryName(path) ?? "", moduleName)),
            path
        );
    }

    internal static AssemblyMetadata Create(ModuleMetadata module) {
        return module is null
            ? throw new ArgumentNullException(nameof(module))
            : new AssemblyMetadata([module]);
    }

    internal static AssemblyMetadata Create(ImmutableArray<ModuleMetadata> modules) {
        for (var i = 0; i < modules.Length; i++) {
            if (modules[i] is null)
                throw new ArgumentNullException(nameof(modules) + "[" + i + "]");
        }

        return new AssemblyMetadata(modules);
    }

    internal static AssemblyMetadata Create(IEnumerable<ModuleMetadata> modules) {
        return Create(modules.AsImmutableOrNull());
    }

    internal static AssemblyMetadata Create(params ModuleMetadata[] modules) {
        return Create(ImmutableArray.CreateRange(modules));
    }

    internal new AssemblyMetadata Copy() {
        return new AssemblyMetadata(this, shareCachedSymbols: true);
    }

    internal AssemblyMetadata CopyWithoutSharingCachedSymbols() {
        return new AssemblyMetadata(this, shareCachedSymbols: false);
    }

    private protected override Metadata CommonCopy() {
        return Copy();
    }

    internal ImmutableArray<ModuleMetadata> GetModules() {
        if (_lazyPublishedModules.IsDefault) {
            var data = GetOrCreateData();
            var newModules = data.modules;

            if (!isImageOwner)
                newModules = newModules.SelectAsArray(module => module.Copy());

            ImmutableInterlocked.InterlockedInitialize(ref _lazyPublishedModules, newModules);
        }

        return _lazyData == Data.Disposed
            ? throw new ObjectDisposedException(nameof(AssemblyMetadata))
            : _lazyPublishedModules;
    }

    internal PEAssembly GetAssembly() {
        return GetOrCreateData().assembly;
    }

    private Data GetOrCreateData() {
        if (_lazyData is null) {
            var modules = _initialModules;
            ImmutableArray<ModuleMetadata>.Builder moduleBuilder = null;

            var createdModulesUsed = false;

            try {
                if (_moduleFactoryOpt is not null) {
                    var additionalModuleNames = _initialModules[0].GetModuleNames();
                    if (additionalModuleNames.Length > 0) {
                        moduleBuilder = ImmutableArray.CreateBuilder<ModuleMetadata>(1 + additionalModuleNames.Length);
                        moduleBuilder.Add(_initialModules[0]);

                        foreach (var moduleName in additionalModuleNames)
                            moduleBuilder.Add(_moduleFactoryOpt(moduleName));

                        modules = moduleBuilder.ToImmutable();
                    }
                }

                var assembly = new PEAssembly(this, modules.SelectAsArray(m => m.module));
                var newData = new Data(modules, assembly);

                createdModulesUsed = Interlocked.CompareExchange(ref _lazyData, newData, null) is null;
            } finally {
                if (moduleBuilder is not null && !createdModulesUsed) {
                    for (var i = _initialModules.Length; i < moduleBuilder.Count; i++)
                        moduleBuilder[i].Dispose();
                }
            }
        }

        return _lazyData.isDisposed ? throw new ObjectDisposedException(nameof(AssemblyMetadata)) : _lazyData;
    }

    public override void Dispose() {
        var previousData = Interlocked.Exchange(ref _lazyData, Data.Disposed);

        if (previousData == Data.Disposed || !isImageOwner)
            return;

        foreach (var module in _initialModules)
            module.Dispose();

        if (previousData is null)
            return;

        for (var i = _initialModules.Length; i < previousData.modules.Length; i++)
            previousData.modules[i].Dispose();
    }

    internal bool IsValidAssembly() {
        var modules = GetModules();

        if (!modules[0].module.isManifestModule)
            return false;

        for (var i = 1; i < modules.Length; i++) {
            var module = modules[i].module;

            if (!module.isLinkedModule && module.metadataReader.MetadataKind != MetadataKind.WindowsMetadata)
                return false;
        }

        return true;
    }

    internal override MetadataImageKind kind => MetadataImageKind.Assembly;
}
