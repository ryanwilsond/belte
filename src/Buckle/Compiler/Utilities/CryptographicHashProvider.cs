using System.Collections.Immutable;
using System.Linq;
using System.Security.Cryptography;

namespace Buckle.Utilities;

internal abstract class CryptographicHashProvider {
    internal abstract ImmutableArray<byte> ComputeHash(HashAlgorithm algorithm);

    internal static ImmutableArray<byte> ComputeSha1(ImmutableArray<byte> bytes) {
        return ComputeSha1(bytes.ToArray());
    }

    internal static ImmutableArray<byte> ComputeSha1(byte[] bytes) {
        return ImmutableArray.Create(SHA1.HashData(bytes));
    }
}
