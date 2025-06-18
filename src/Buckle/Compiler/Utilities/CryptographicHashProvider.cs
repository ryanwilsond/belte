using System.Collections.Immutable;
using System.Linq;
using System.Security.Cryptography;

namespace Buckle.Utilities;

internal static class CryptographicHashProvider {
    internal static ImmutableArray<byte> ComputeSha1(ImmutableArray<byte> bytes) {
        return ComputeSha1(bytes.ToArray());
    }

    internal static ImmutableArray<byte> ComputeSha1(byte[] bytes) {
        return ImmutableArray.Create(SHA1.HashData(bytes));
    }
}
