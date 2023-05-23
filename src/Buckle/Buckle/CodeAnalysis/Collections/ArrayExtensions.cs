
namespace Buckle.CodeAnalysis;

/// <summary>
/// Extensions on the System.Array class.
/// </summary>
internal static class ArrayExtensions {
    /// <summary>
    /// Performs a binary search to find a value.
    /// </summary>
    internal static int BinarySearch(this int[] array, int value) {
        var low = 0;
        var high = array.Length - 1;

        while (low <= high) {
            var middle = low + ((high - low) >> 1);
            var midValue = array[middle];

            if (midValue == value)
                return middle;
            else if (midValue > value)
                high = middle - 1;
            else
                low = middle + 1;
        }

        return ~low;
    }
}
