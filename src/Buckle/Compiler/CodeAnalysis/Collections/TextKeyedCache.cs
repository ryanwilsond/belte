using System;
using System.Threading;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis;

internal class TextKeyedCache<T> where T : class {
    private class SharedEntryValue {
        public readonly string Text;
        public readonly T Item;

        public SharedEntryValue(string Text, T item) {
            this.Text = Text;
            this.Item = item;
        }
    }

    private const int LocalSizeBits = 11;
    private const int LocalSize = (1 << LocalSizeBits);
    private const int LocalSizeMask = LocalSize - 1;

    private const int SharedSizeBits = 16;
    private const int SharedSize = (1 << SharedSizeBits);
    private const int SharedSizeMask = SharedSize - 1;

    private const int SharedBucketBits = 4;
    private const int SharedBucketSize = (1 << SharedBucketBits);
    private const int SharedBucketSizeMask = SharedBucketSize - 1;

    private readonly (string Text, int HashCode, T Item)[] _localTable = new (string Text, int HashCode, T Item)[LocalSize];

    private static readonly (int HashCode, SharedEntryValue Entry)[] s_sharedTable = new (int HashCode, SharedEntryValue Entry)[SharedSize];

    private readonly (int HashCode, SharedEntryValue Entry)[] _sharedTableInst = s_sharedTable;

    private readonly StringTable _strings;

    private Random? _random;

    internal TextKeyedCache()
        : this(null) {
    }

    #region "Poolable"

    private TextKeyedCache(ObjectPool<TextKeyedCache<T>>? pool) {
        _pool = pool;
        _strings = new StringTable();
    }

    private readonly ObjectPool<TextKeyedCache<T>>? _pool;
    private static readonly ObjectPool<TextKeyedCache<T>> StaticPool = CreatePool();

    private static ObjectPool<TextKeyedCache<T>> CreatePool() {
        var pool = new ObjectPool<TextKeyedCache<T>>(
            pool => new TextKeyedCache<T>(pool),
            Environment.ProcessorCount * 4);
        return pool;
    }

    public static TextKeyedCache<T> GetInstance() {
        return StaticPool.Allocate();
    }

    public void Free() {
        _pool?.Free(this);
    }

    #endregion

    internal T? FindItem(char[] chars, int start, int len, int hashCode) {
        ref var localSlot = ref _localTable[LocalIdxFromHash(hashCode)];

        var text = localSlot.Text;

        if (text is not null && localSlot.HashCode == hashCode) {
            if (StringTable.TextEquals(text, chars.AsSpan(start, len)))
                return localSlot.Item;
        }

        var e = FindSharedEntry(chars, start, len, hashCode);

        if (e is not null) {
            localSlot.HashCode = hashCode;
            localSlot.Text = e.Text;

            var tk = e.Item;
            localSlot.Item = tk;

            return tk;
        }

        return null!;
    }

    private SharedEntryValue? FindSharedEntry(char[] chars, int start, int len, int hashCode) {
        var arr = _sharedTableInst;
        var idx = SharedIdxFromHash(hashCode);

        SharedEntryValue? e = null;
        int hash;

        for (var i = 1; i < SharedBucketSize + 1; i++) {
            (hash, e) = arr[idx];

            if (e is not null) {
                if (hash == hashCode && StringTable.TextEquals(e.Text, chars.AsSpan(start, len)))
                    break;

                e = null;
            } else {
                break;
            }

            idx = (idx + i) & SharedSizeMask;
        }

        return e;
    }

    internal void AddItem(char[] chars, int start, int len, int hashCode, T item) {
        var text = _strings.Add(chars, start, len);

        var e = new SharedEntryValue(text, item);
        AddSharedEntry(hashCode, e);

        ref var localSlot = ref _localTable[LocalIdxFromHash(hashCode)];
        localSlot.HashCode = hashCode;
        localSlot.Text = text;
        localSlot.Item = item;
    }

    private void AddSharedEntry(int hashCode, SharedEntryValue e) {
        var arr = _sharedTableInst;
        var idx = SharedIdxFromHash(hashCode);

        var curIdx = idx;

        for (var i = 1; i < SharedBucketSize + 1; i++) {
            if (arr[curIdx].Entry is null) {
                idx = curIdx;
                goto foundIdx;
            }

            curIdx = (curIdx + i) & SharedSizeMask;
        }

        var i1 = NextRandom() & SharedBucketSizeMask;
        idx = (idx + ((i1 * i1 + i1) / 2)) & SharedSizeMask;

foundIdx:
        arr[idx].HashCode = hashCode;
        Volatile.Write(ref arr[idx].Entry, e);
    }

    private static int LocalIdxFromHash(int hash) {
        return hash & LocalSizeMask;
    }

    private static int SharedIdxFromHash(int hash) {
        return (hash ^ (hash >> LocalSizeBits)) & SharedSizeMask;
    }

    private int NextRandom() {
        var r = _random;

        if (r is not null)
            return r.Next();

        r = new Random();
        _random = r;
        return r.Next();
    }
}
