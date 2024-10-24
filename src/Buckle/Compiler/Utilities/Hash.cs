
namespace Buckle.Utilities;

/// <summary>
/// Computes hashes, https://github.com/dotnet/roslyn/blob/main/src/Compilers/Core/Portable/InternalUtilities/Hash.cs
/// </summary>
internal static class Hash {
    internal const int FnvOffsetBias = unchecked((int)2166136261);

    internal const int FnvPrime = 16777619;

    internal static int CombineFNVHash(int hashCode, string text) {
        foreach (var ch in text)
            hashCode = unchecked((hashCode ^ ch) * FnvPrime);

        return hashCode;
    }

    internal static int GetFNVHashCode(string text) {
        return CombineFNVHash(FnvOffsetBias, text);
    }

    internal static int Combine(int newKey, int currentKey) {
        return unchecked((currentKey * (int)0xA5555529) + newKey);
    }

    internal static int Combine<T>(T newKeyPart, int currentKey) where T : class? {
        var hash = unchecked(currentKey * (int)0xA5555529);

        if (newKeyPart is not null) {
            return unchecked(hash + newKeyPart.GetHashCode());
        }

        return hash;
    }
}
