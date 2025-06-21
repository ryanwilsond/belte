using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;

namespace Buckle.Utilities;

internal abstract class CryptographicHashProvider {
    private ImmutableArray<byte> _lazySHA1Hash;
    private ImmutableArray<byte> _lazySHA256Hash;
    private ImmutableArray<byte> _lazySHA384Hash;
    private ImmutableArray<byte> _lazySHA512Hash;
    private ImmutableArray<byte> _lazyMD5Hash;

    internal abstract ImmutableArray<byte> ComputeHash(HashAlgorithm algorithm);

    internal ImmutableArray<byte> GetHash(AssemblyHashAlgorithm algorithmId) {
        using var algorithm = TryGetAlgorithm(algorithmId);

        if (algorithm is null)
            return [];

        return algorithmId switch {
            AssemblyHashAlgorithm.None or AssemblyHashAlgorithm.Sha1 => GetHash(ref _lazySHA1Hash, algorithm),
            AssemblyHashAlgorithm.Sha256 => GetHash(ref _lazySHA256Hash, algorithm),
            AssemblyHashAlgorithm.Sha384 => GetHash(ref _lazySHA384Hash, algorithm),
            AssemblyHashAlgorithm.Sha512 => GetHash(ref _lazySHA512Hash, algorithm),
            AssemblyHashAlgorithm.MD5 => GetHash(ref _lazyMD5Hash, algorithm),
            _ => throw ExceptionUtilities.UnexpectedValue(algorithmId),
        };
    }

    internal static HashAlgorithm? TryGetAlgorithm(AssemblyHashAlgorithm algorithmId) {
        return algorithmId switch {
            AssemblyHashAlgorithm.None or AssemblyHashAlgorithm.Sha1 => SHA1.Create(),
            AssemblyHashAlgorithm.Sha256 => SHA256.Create(),
            AssemblyHashAlgorithm.Sha384 => SHA384.Create(),
            AssemblyHashAlgorithm.Sha512 => SHA512.Create(),
            AssemblyHashAlgorithm.MD5 => MD5.Create(),
            _ => null,
        };
    }

    private ImmutableArray<byte> GetHash(ref ImmutableArray<byte> lazyHash, HashAlgorithm algorithm) {
        if (lazyHash.IsDefault)
            ImmutableInterlocked.InterlockedCompareExchange(ref lazyHash, ComputeHash(algorithm), default);

        return lazyHash;
    }

    internal static ImmutableArray<byte> ComputeSha1(ImmutableArray<byte> bytes) {
        return ComputeSha1(bytes.ToArray());
    }

    internal static ImmutableArray<byte> ComputeSha1(byte[] bytes) {
        return ImmutableArray.Create(SHA1.HashData(bytes));
    }
}
