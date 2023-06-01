
namespace Buckle.Utilities;

/// <summary>
/// Computes hashes, https://github.com/dotnet/roslyn/blob/main/src/Compilers/Core/Portable/InternalUtilities/Hash.cs
/// </summary>
internal static class Hash {
    internal const int FnvOffsetBias = unchecked((int)2166136261);

    internal const int FnvPrime = 16777619;

    internal static int GetFNVHashCode(byte[] data) {
        int hashCode = Hash.FnvOffsetBias;

        for (int i = 0; i < data.Length; i++)
            hashCode = unchecked((hashCode ^ data[i]) * Hash.FnvPrime);

        return hashCode;
    }

    internal static int CombineFNVHash(int hashCode, string text) {
        foreach (char ch in text)
            hashCode = unchecked((hashCode ^ ch) * Hash.FnvPrime);

        return hashCode;
    }

    internal static int GetFNVHashCode(string text) {
        return CombineFNVHash(Hash.FnvOffsetBias, text);
    }

    internal static int Combine(int newKey, int currentKey) {
        return unchecked((currentKey * (int)0xA5555529) + newKey);
    }

    internal static int Combine<T>(T newKeyPart, int currentKey) where T : class? {
        int hash = unchecked(currentKey * (int)0xA5555529);

        if (newKeyPart != null) {
            return unchecked(hash + newKeyPart.GetHashCode());
        }

        return hash;
    }
}
