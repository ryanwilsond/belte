using System;
using System.Collections.Immutable;

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

    internal static int CombineFNVHash(int hashCode, ReadOnlySpan<char> data) {
        for (var i = 0; i < data.Length; i++)
            hashCode = unchecked((hashCode ^ data[i]) * FnvPrime);

        return hashCode;
    }

    internal static int CombineFNVHash(int hashCode, char ch) {
        return unchecked((hashCode ^ ch) * FnvPrime);
    }

    internal static int GetFNVHashCode(string text) {
        return CombineFNVHash(FnvOffsetBias, text);
    }

    internal static int GetFNVHashCode(char ch) {
        return CombineFNVHash(FnvOffsetBias, ch);
    }

    internal static int GetFNVHashCode(ReadOnlySpan<char> data) {
        return CombineFNVHash(FnvOffsetBias, data);
    }

    internal static int GetFNVHashCode(System.Text.StringBuilder text) {
        var hashCode = FnvOffsetBias;

        foreach (var chunk in text.GetChunks())
            hashCode = CombineFNVHash(hashCode, chunk.Span);

        return hashCode;
    }

    internal static int GetFNVHashCode(string text, int start, int length)
        => GetFNVHashCode(text.AsSpan(start, length));

    internal static int GetFNVHashCode(char[] text, int start, int length) {
        var hashCode = FnvOffsetBias;
        var end = start + length;

        for (var i = start; i < end; i++)
            hashCode = unchecked((hashCode ^ text[i]) * FnvPrime);

        return hashCode;
    }

    internal static int GetFNVHashCode(ReadOnlySpan<byte> data, out bool isAscii) {
        var hashCode = FnvOffsetBias;
        byte asciiMask = 0;

        for (var i = 0; i < data.Length; i++) {
            var b = data[i];
            asciiMask |= b;
            hashCode = unchecked((hashCode ^ b) * FnvPrime);
        }

        isAscii = (asciiMask & 0x80) == 0;
        return hashCode;
    }

    internal static int GetFNVHashCode(byte[] data) {
        var hashCode = FnvOffsetBias;

        for (var i = 0; i < data.Length; i++)
            hashCode = unchecked((hashCode ^ data[i]) * FnvPrime);

        return hashCode;
    }

    internal static int GetFNVHashCode(ImmutableArray<byte> data) {
        var hashCode = FnvOffsetBias;

        for (var i = 0; i < data.Length; i++)
            hashCode = unchecked((hashCode ^ data[i]) * FnvPrime);

        return hashCode;
    }

    internal static int Combine(int newKey, int currentKey) {
        return unchecked((currentKey * (int)0xA5555529) + newKey);
    }

    internal static int Combine(bool newKeyPart, int currentKey) {
        return Combine(currentKey, newKeyPart ? 1 : 0);
    }

    internal static int Combine<T>(T newKeyPart, int currentKey) where T : class? {
        var hash = unchecked(currentKey * (int)0xA5555529);

        if (newKeyPart is not null)
            return unchecked(hash + newKeyPart.GetHashCode());

        return hash;
    }
}
