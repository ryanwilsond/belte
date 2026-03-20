using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Threading;

namespace Buckle.CodeAnalysis;

internal sealed partial class ModuleMetadata : Metadata {
    private readonly PEModule _module;
    private Action _onDispose;
    private bool _isDisposed;

    private ModuleMetadata(PEReader peReader, Action onDispose)
        : base(isImageOwner: true, id: MetadataId.CreateNewId()) {
        _module = new PEModule(
            this,
            peReader: peReader,
            metadataOpt: IntPtr.Zero,
            metadataSizeOpt: 0,
            includeEmbeddedInteropTypes: false,
            ignoreAssemblyRefs: false
        );

        _onDispose = onDispose;
    }

    private ModuleMetadata(
        IntPtr metadata,
        int size,
        Action onDispose,
        bool includeEmbeddedInteropTypes,
        bool ignoreAssemblyRefs)
        : base(isImageOwner: true, id: MetadataId.CreateNewId()) {
        _module = new PEModule(
            this,
            peReader: null,
            metadataOpt: metadata,
            metadataSizeOpt: size,
            includeEmbeddedInteropTypes: includeEmbeddedInteropTypes,
            ignoreAssemblyRefs: ignoreAssemblyRefs
        );

        _onDispose = onDispose;
    }

    private ModuleMetadata(ModuleMetadata metadata) : base(isImageOwner: false, id: metadata.id) {
        _module = metadata.module;
    }

    internal static ModuleMetadata CreateFromMetadata(nint metadata, int size)
        => CreateFromMetadataWorker(metadata, size, onDispose: null);

    internal static unsafe ModuleMetadata CreateFromMetadata(nint metadata, int size, Action onDispose) {
        return onDispose is null
            ? throw new ArgumentNullException(nameof(onDispose))
            : CreateFromMetadataWorker(metadata, size, onDispose);
    }

    private static ModuleMetadata CreateFromMetadataWorker(nint metadata, int size, Action onDispose) {
        return new ModuleMetadata(metadata, size, onDispose, false, false);
    }

    internal static ModuleMetadata CreateFromMetadata(
        IntPtr metadata,
        int size,
        bool includeEmbeddedInteropTypes,
        bool ignoreAssemblyRefs = false) {
        return new ModuleMetadata(metadata, size, onDispose: null, includeEmbeddedInteropTypes, ignoreAssemblyRefs);
    }

    internal static ModuleMetadata CreateFromFile(string path) {
        return CreateFromStream(File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read));
    }

    internal static unsafe ModuleMetadata CreateFromImage(nint peImage, int size)
        => CreateFromImage((byte*)peImage, size, onDispose: null);

    private static unsafe ModuleMetadata CreateFromImage(byte* peImage, int size, Action onDispose) {
        return new ModuleMetadata(new PEReader(peImage, size), onDispose);
    }

    internal static ModuleMetadata CreateFromImage(IEnumerable<byte> peImage) {
        return peImage is null ? throw new ArgumentNullException(nameof(peImage)) : CreateFromImage([.. peImage]);
    }

    internal static ModuleMetadata CreateFromImage(ImmutableArray<byte> peImage) {
        if (peImage.IsDefault)
            throw new ArgumentNullException(nameof(peImage));

        return new ModuleMetadata(new PEReader(peImage), onDispose: null);
    }

    internal static ModuleMetadata CreateFromStream(Stream peStream, bool leaveOpen = false) {
        return CreateFromStream(peStream, leaveOpen ? PEStreamOptions.LeaveOpen : PEStreamOptions.Default);
    }

    internal static ModuleMetadata CreateFromStream(Stream peStream, PEStreamOptions options) {
        if (peStream is null)
            throw new ArgumentNullException(nameof(peStream));

        var prefetch = (options & (PEStreamOptions.PrefetchEntireImage | PEStreamOptions.PrefetchMetadata)) != 0;

        if (!prefetch && peStream is UnmanagedMemoryStream unmanagedMemoryStream) {
            unsafe {
                Action onDispose = options.HasFlag(PEStreamOptions.LeaveOpen)
                    ? null
                    : unmanagedMemoryStream.Dispose;

                return CreateFromImage(
                    unmanagedMemoryStream.PositionPointer,
                    (int)Math.Min(unmanagedMemoryStream.Length, int.MaxValue),
                    onDispose);
            }
        }

        if (peStream.Length == 0 && (options & PEStreamOptions.PrefetchEntireImage) != 0 &&
            (options & PEStreamOptions.PrefetchMetadata) != 0) {
            _ = new PEHeaders(peStream);
        }

        return new ModuleMetadata(new PEReader(peStream, options), onDispose: null);
    }

    internal new ModuleMetadata Copy() {
        return new ModuleMetadata(this);
    }

    private protected override Metadata CommonCopy() {
        return Copy();
    }

    public override void Dispose() {
        _isDisposed = true;

        if (isImageOwner) {
            _module.Dispose();

            var onDispose = Interlocked.Exchange(ref _onDispose, null);
            onDispose?.Invoke();
        }
    }

    internal bool isDisposed => _isDisposed || _module.isDisposed;

    internal PEModule module => isDisposed ? throw new ObjectDisposedException(nameof(ModuleMetadata)) : _module;

    internal string name => module.name;

    internal Guid GetModuleVersionId() {
        return module.GetModuleVersionIdOrThrow();
    }

    internal override MetadataImageKind kind => MetadataImageKind.Module;

    internal ImmutableArray<string> GetModuleNames() {
        return module.GetMetadataModuleNamesOrThrow();
    }

    internal MetadataReader GetMetadataReader() => metadataReader;

    internal MetadataReader metadataReader => module.metadataReader;
}
