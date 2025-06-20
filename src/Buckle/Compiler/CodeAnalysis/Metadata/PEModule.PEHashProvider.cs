using System;
using System.Collections.Immutable;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis;

internal sealed partial class PEModule {
    private sealed class PEHashProvider : CryptographicHashProvider {
        private readonly PEReader _peReader;

        internal PEHashProvider(PEReader peReader) {
            _peReader = peReader;
        }

        internal override unsafe ImmutableArray<byte> ComputeHash(HashAlgorithm algorithm) {
            var block = _peReader.GetEntireImage();
            byte[] hash;

            using (var stream = new ReadOnlyUnmanagedMemoryStream(_peReader, (IntPtr)block.Pointer, block.Length))
                hash = algorithm.ComputeHash(stream);

            return ImmutableArray.Create(hash);
        }
    }
}
