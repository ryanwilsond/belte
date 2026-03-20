namespace Buckle.CodeAnalysis;

internal readonly partial struct SegmentedArray<T> {
    /// <summary>
    /// Private helper class for use only by <see cref="SegmentedCollectionsMarshal"/>.
    /// </summary>
    internal static class PrivateMarshal {
        /// <inheritdoc cref="SegmentedCollectionsMarshal.AsSegments{T}(SegmentedArray{T})"/>
        public static T[][] AsSegments(SegmentedArray<T> array)
            => array._items;
    }
}
