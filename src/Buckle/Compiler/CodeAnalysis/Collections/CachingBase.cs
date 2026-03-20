
namespace Buckle.CodeAnalysis;

internal abstract class CachingBase<TEntry> {
    private readonly int _alignedSize;

    private TEntry[] _entries;

    private protected readonly int _mask;

    private protected TEntry[] _lazyEntries => _entries ??= new TEntry[_alignedSize];

    internal CachingBase(int size, bool createBackingArray = true) {
        _alignedSize = AlignSize(size);
        _mask = _alignedSize - 1;
        _entries = createBackingArray ? new TEntry[_alignedSize] : null;
    }

    private static int AlignSize(int size) {
        size--;
        size |= size >> 1;
        size |= size >> 2;
        size |= size >> 4;
        size |= size >> 8;
        size |= size >> 16;

        return size + 1;
    }
}
