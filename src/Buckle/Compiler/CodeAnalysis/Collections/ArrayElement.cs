
namespace Buckle.CodeAnalysis;

/// <summary>
/// A better array item as "elements[i].Value = v" is much better than "elements[i] = (ArrayElement<T>)v".
/// </summary>
internal struct ArrayElement<T> {
    internal T Value;

    public static implicit operator T(ArrayElement<T> element) {
        return element.Value;
    }

    /// <summary>
    /// Creates an array of ArrayElements from a basic array.
    /// </summary>
    internal static ArrayElement<T>[] MakeElementArray(T[] items) {
        if (items == null)
            return null;

        var array = new ArrayElement<T>[items.Length];

        for (var i = 0; i < items.Length; i++)
            array[i].Value = items[i];

        return array;
    }

    /// <summary>
    /// Creates a basic array from an array of ArrayElements.
    /// </summary>
    /// <param name="items"></param>
    /// <returns></returns>
    internal static T[] MakeArray(ArrayElement<T>[] items) {
        if (items == null)
            return null;

        var array = new T[items.Length];

        for (var i = 0; i < items.Length; i++)
            array[i] = items[i].Value;

        return array;
    }
}