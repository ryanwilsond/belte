using System;
using System.Text;
using System.Threading;
using Buckle.CodeAnalysis;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.Utilities;

internal partial class StringTable {
    private static readonly SegmentedArray<Entry> SharedTable = new SegmentedArray<Entry>(SharedSize);
    private static readonly ObjectPool<StringTable> StaticPool = CreatePool();
    private static int SharedRandom = Environment.TickCount;

    private const int LocalSizeBits = 11;
    private const int LocalSize = 1 << LocalSizeBits;
    private const int LocalSizeMask = LocalSize - 1;

    private const int SharedSizeBits = 16;
    private const int SharedSize = 1 << SharedSizeBits;
    private const int SharedSizeMask = SharedSize - 1;

    private const int SharedBucketBits = 4;
    private const int SharedBucketSize = 1 << SharedBucketBits;
    private const int SharedBucketSizeMask = SharedBucketSize - 1;

    private readonly Entry[] _localTable = new Entry[LocalSize];

    private readonly ObjectPool<StringTable> _pool;

    private int _localRandom = Environment.TickCount;

    internal StringTable() : this(null) { }

    private StringTable(ObjectPool<StringTable> pool) {
        _pool = pool;
    }

    private static ObjectPool<StringTable> CreatePool() {
        var pool = new ObjectPool<StringTable>(pool => new StringTable(pool), Environment.ProcessorCount * 2);
        return pool;
    }

    public static StringTable GetInstance() {
        return StaticPool.Allocate();
    }

    public void Free() {
        _pool?.Free(this);
    }

    internal string Add(char[] chars, int start, int len) {
        var span = chars.AsSpan(start, len);
        var hashCode = Hash.GetFNVHashCode(chars, start, len);

        var arr = _localTable;
        var idx = LocalIdxFromHash(hashCode);

        var text = arr[idx].text;

        if (text is not null && arr[idx].hashCode == hashCode) {
            var result = arr[idx].text;

            if (TextEquals(result, span))
                return result;
        }

        var shared = FindSharedEntry(chars, start, len, hashCode);

        if (shared is not null) {
            arr[idx].hashCode = hashCode;
            arr[idx].text = shared;

            return shared;
        }

        return AddItem(chars, start, len, hashCode);
    }

    internal string Add(string chars, int start, int len) {
        var hashCode = Hash.GetFNVHashCode(chars, start, len);

        var arr = _localTable;
        var idx = LocalIdxFromHash(hashCode);

        var text = arr[idx].text;

        if (text is not null && arr[idx].hashCode == hashCode) {
            var result = arr[idx].text;

            if (TextEquals(result, chars, start, len))
                return result;
        }

        var shared = FindSharedEntry(chars, start, len, hashCode);

        if (shared is not null) {
            arr[idx].hashCode = hashCode;
            arr[idx].text = shared;

            return shared;
        }

        return AddItem(chars, start, len, hashCode);
    }

    internal string Add(char chars) {
        var hashCode = Hash.GetFNVHashCode(chars);

        var arr = _localTable;
        var idx = LocalIdxFromHash(hashCode);

        var text = arr[idx].text;

        if (text is not null) {
            var result = arr[idx].text;

            if (text.Length == 1 && text[0] == chars)
                return result;
        }

        var shared = FindSharedEntry(chars, hashCode);

        if (shared is not null) {
            arr[idx].hashCode = hashCode;
            arr[idx].text = shared;

            return shared;
        }

        return AddItem(chars, hashCode);
    }

    internal string Add(StringBuilder chars) {
        var hashCode = Hash.GetFNVHashCode(chars);

        var arr = _localTable;
        var idx = LocalIdxFromHash(hashCode);

        var text = arr[idx].text;

        if (text is not null && arr[idx].hashCode == hashCode) {
            var result = arr[idx].text;

            if (TextEquals(result, chars))
                return result;
        }

        var shared = FindSharedEntry(chars, hashCode);

        if (shared is not null) {
            arr[idx].hashCode = hashCode;
            arr[idx].text = shared;

            return shared;
        }

        return AddItem(chars, hashCode);
    }

    internal string Add(string chars) {
        var hashCode = Hash.GetFNVHashCode(chars);

        var arr = _localTable;
        var idx = LocalIdxFromHash(hashCode);

        var text = arr[idx].text;

        if (text is not null && arr[idx].hashCode == hashCode) {
            var result = arr[idx].text;
            if (result == chars) {
                return result;
            }
        }

        var shared = FindSharedEntry(chars, hashCode);

        if (shared is not null) {
            arr[idx].hashCode = hashCode;
            arr[idx].text = shared;

            return shared;
        }

        AddCore(chars, hashCode);
        return chars;
    }

    private static string FindSharedEntry(char[] chars, int start, int len, int hashCode) {
        var arr = SharedTable;
        var idx = SharedIdxFromHash(hashCode);

        string e = null;

        for (var i = 1; i < SharedBucketSize + 1; i++) {
            e = arr[idx].text;
            var hash = arr[idx].hashCode;

            if (e is not null) {
                if (hash == hashCode && TextEquals(e, chars.AsSpan(start, len)))
                    break;

                e = null;
            } else {
                break;
            }

            idx = (idx + i) & SharedSizeMask;
        }

        return e;
    }

    private static string FindSharedEntry(string chars, int start, int len, int hashCode) {
        var arr = SharedTable;
        var idx = SharedIdxFromHash(hashCode);

        string e = null;

        for (var i = 1; i < SharedBucketSize + 1; i++) {
            e = arr[idx].text;
            var hash = arr[idx].hashCode;

            if (e is not null) {
                if (hash == hashCode && TextEquals(e, chars, start, len))
                    break;

                e = null;
            } else {
                break;
            }

            idx = (idx + i) & SharedSizeMask;
        }

        return e;
    }

    private static string FindSharedEntryASCII(int hashCode, ReadOnlySpan<byte> asciiChars) {
        var arr = SharedTable;
        var idx = SharedIdxFromHash(hashCode);

        string e = null;

        for (var i = 1; i < SharedBucketSize + 1; i++) {
            e = arr[idx].text;
            var hash = arr[idx].hashCode;

            if (e is not null) {
                if (hash == hashCode && TextEqualsASCII(e, asciiChars))
                    break;

                e = null;
            } else {
                break;
            }

            idx = (idx + i) & SharedSizeMask;
        }

        return e;
    }

    private static string FindSharedEntry(char chars, int hashCode) {
        var arr = SharedTable;
        var idx = SharedIdxFromHash(hashCode);

        string e = null;

        for (var i = 1; i < SharedBucketSize + 1; i++) {
            e = arr[idx].text;

            if (e is not null) {
                if (e.Length == 1 && e[0] == chars)
                    break;

                e = null;
            } else {
                break;
            }

            idx = (idx + i) & SharedSizeMask;
        }

        return e;
    }

    private static string FindSharedEntry(StringBuilder chars, int hashCode) {
        var arr = SharedTable;
        var idx = SharedIdxFromHash(hashCode);

        string e = null;

        for (var i = 1; i < SharedBucketSize + 1; i++) {
            e = arr[idx].text;
            var hash = arr[idx].hashCode;

            if (e is not null) {
                if (hash == hashCode && TextEquals(e, chars))
                    break;

                e = null;
            } else {
                break;
            }

            idx = (idx + i) & SharedSizeMask;
        }

        return e;
    }

    private static string FindSharedEntry(string chars, int hashCode) {
        var arr = SharedTable;
        var idx = SharedIdxFromHash(hashCode);

        string e = null;

        for (var i = 1; i < SharedBucketSize + 1; i++) {
            e = arr[idx].text;
            var hash = arr[idx].hashCode;

            if (e is not null) {
                if (hash == hashCode && e == chars)
                    break;

                e = null;
            } else {
                break;
            }

            idx = (idx + i) & SharedSizeMask;
        }

        return e;
    }

    private string AddItem(char[] chars, int start, int len, int hashCode) {
        var text = new string(chars, start, len);
        AddCore(text, hashCode);
        return text;
    }

    private string AddItem(string chars, int start, int len, int hashCode) {
        var text = chars.Substring(start, len);
        AddCore(text, hashCode);
        return text;
    }

    private string AddItem(char chars, int hashCode) {
        var text = new string(chars, 1);
        AddCore(text, hashCode);
        return text;
    }

    private string AddItem(StringBuilder chars, int hashCode) {
        var text = chars.ToString();
        AddCore(text, hashCode);
        return text;
    }

    private void AddCore(string chars, int hashCode) {
        AddSharedEntry(hashCode, chars);

        var arr = _localTable;
        var idx = LocalIdxFromHash(hashCode);
        arr[idx].hashCode = hashCode;
        arr[idx].text = chars;
    }

    private void AddSharedEntry(int hashCode, string text) {
        var arr = SharedTable;
        var idx = SharedIdxFromHash(hashCode);

        var curIdx = idx;

        for (var i = 1; i < SharedBucketSize + 1; i++) {
            if (arr[curIdx].text is null) {
                idx = curIdx;
                goto foundIdx;
            }

            curIdx = (curIdx + i) & SharedSizeMask;
        }

        var i1 = LocalNextRandom() & SharedBucketSizeMask;
        idx = (idx + ((i1 * i1 + i1) / 2)) & SharedSizeMask;

foundIdx:
        arr[idx].hashCode = hashCode;
        Volatile.Write(ref arr[idx].text, text);
    }

    internal static string AddShared(StringBuilder chars) {
        var hashCode = Hash.GetFNVHashCode(chars);

        var shared = FindSharedEntry(chars, hashCode);

        if (shared is not null)
            return shared;

        return AddSharedSlow(hashCode, chars);
    }

    private static string AddSharedSlow(int hashCode, StringBuilder builder) {
        var text = builder.ToString();
        AddSharedSlow(hashCode, text);
        return text;
    }

    internal static string AddSharedUtf8(ReadOnlySpan<byte> bytes) {
        var hashCode = Hash.GetFNVHashCode(bytes, out var isAscii);

        if (isAscii) {
            var shared = FindSharedEntryASCII(hashCode, bytes);

            if (shared is not null)
                return shared;
        }

        return AddSharedSlow(hashCode, bytes, isAscii);
    }

    private static string AddSharedSlow(int hashCode, ReadOnlySpan<byte> utf8Bytes, bool isAscii) {
        string text;

        unsafe {
            fixed (byte* bytes = &utf8Bytes.GetPinnableReference()) {
                text = Encoding.UTF8.GetString(bytes, utf8Bytes.Length);
            }
        }

        if (isAscii)
            AddSharedSlow(hashCode, text);

        return text;
    }

    private static void AddSharedSlow(int hashCode, string text) {
        var arr = SharedTable;
        var idx = SharedIdxFromHash(hashCode);

        var curIdx = idx;

        for (var i = 1; i < SharedBucketSize + 1; i++) {
            if (arr[curIdx].text is null) {
                idx = curIdx;
                goto foundIdx;
            }

            curIdx = (curIdx + i) & SharedSizeMask;
        }

        var i1 = SharedNextRandom() & SharedBucketSizeMask;
        idx = (idx + ((i1 * i1 + i1) / 2)) & SharedSizeMask;

foundIdx:
        arr[idx].hashCode = hashCode;
        Volatile.Write(ref arr[idx].text, text);
    }

    private static int LocalIdxFromHash(int hash) {
        return hash & LocalSizeMask;
    }

    private static int SharedIdxFromHash(int hash) {
        return (hash ^ (hash >> LocalSizeBits)) & SharedSizeMask;
    }

    private int LocalNextRandom() {
        return _localRandom++;
    }

    private static int SharedNextRandom() {
        return Interlocked.Increment(ref SharedRandom);
    }

    internal static bool TextEquals(string array, string text, int start, int length) {
        if (array.Length != length)
            return false;

        for (var i = 0; i < array.Length; i++) {
            if (array[i] != text[start + i])
                return false;
        }

        return true;
    }

    internal static bool TextEquals(string array, StringBuilder text) {
        if (array.Length != text.Length)
            return false;

        var chunkOffset = 0;

        foreach (var chunk in text.GetChunks()) {
            if (!chunk.Span.Equals(array.AsSpan().Slice(chunkOffset, chunk.Length), StringComparison.Ordinal))
                return false;

            chunkOffset += chunk.Length;
        }

        return true;
    }

    internal static bool TextEqualsASCII(string text, ReadOnlySpan<byte> ascii) {
        if (ascii.Length != text.Length)
            return false;

        for (var i = 0; i < ascii.Length; i++) {
            if (ascii[i] != text[i])
                return false;
        }

        return true;
    }

    internal static bool TextEquals(string array, ReadOnlySpan<char> text)
        => text.Equals(array.AsSpan(), StringComparison.Ordinal);
}
