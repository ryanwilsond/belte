using System.Threading;

namespace Buckle.CodeAnalysis;

internal class SmallConcurrentSetOfInts {
    private int _v1;
    private int _v2;
    private int _v3;
    private int _v4;
    private SmallConcurrentSetOfInts _next;

    private const int _unoccupied = int.MinValue;

    internal SmallConcurrentSetOfInts() {
        _v1 = _v2 = _v3 = _v4 = _unoccupied;
    }

    private SmallConcurrentSetOfInts(int initialValue) {
        _v1 = initialValue;
        _v2 = _v3 = _v4 = _unoccupied;
    }

    internal bool Contains(int i) {
        return Contains(this, i);
    }

    private static bool Contains(SmallConcurrentSetOfInts set, int i) {
        var current = set;

        do {
            if (current._v1 == i || current._v2 == i || current._v3 == i || current._v4 == i)
                return true;

            current = current._next;
        } while (current is not null);

        return false;
    }

    internal bool Add(int i) {
        return Add(this, i);
    }

    private static bool Add(SmallConcurrentSetOfInts set, int i) {
        var added = false;

        while (true) {
            if (AddHelper(ref set._v1, i, ref added) ||
                AddHelper(ref set._v2, i, ref added) ||
                AddHelper(ref set._v3, i, ref added) ||
                AddHelper(ref set._v4, i, ref added)) {
                return added;
            }

            var nextSet = set._next;

            if (nextSet is null) {
                var tail = new SmallConcurrentSetOfInts(initialValue: i);

                nextSet = Interlocked.CompareExchange(ref set._next, tail, null);

                if (nextSet is null)
                    return true;
            }

            set = nextSet;
        }
    }

    private static bool AddHelper(ref int slot, int i, ref bool added) {
        var val = slot;

        if (val == _unoccupied) {
            val = Interlocked.CompareExchange(ref slot, i, _unoccupied);

            if (val == _unoccupied) {
                added = true;
                return true;
            }
        }

        return val == i;
    }
}
