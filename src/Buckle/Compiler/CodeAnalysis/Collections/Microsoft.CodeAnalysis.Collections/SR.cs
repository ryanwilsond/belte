using System.Reflection;

namespace Buckle.CodeAnalysis;

internal static class Strings { }

internal static partial class SR {
    private static global::System.Resources.ResourceManager s_resourceManager;
    internal static global::System.Resources.ResourceManager ResourceManager => s_resourceManager ?? (s_resourceManager = new global::System.Resources.ResourceManager(typeof(Strings)));
    internal static global::System.Globalization.CultureInfo Culture { get; set; }
#if !NET20
    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
#endif
    internal static string GetResourceString(string resourceKey, string defaultValue = null) => ResourceManager.GetString(resourceKey, Culture);
    /// <summary>Destination array is not long enough to copy all the items in the collection. Check array index and length.</summary>
    internal static string @Arg_ArrayPlusOffTooSmall => GetResourceString("Arg_ArrayPlusOffTooSmall");
    /// <summary>Hashtable's capacity overflowed and went negative. Check load factor, capacity and the current size of the table.</summary>
    internal static string @Arg_HTCapacityOverflow => GetResourceString("Arg_HTCapacityOverflow");
    /// <summary>The given key '{0}' was not present in the dictionary.</summary>
    internal static string @Arg_KeyNotFoundWithKey => GetResourceString("Arg_KeyNotFoundWithKey");
    /// <summary>Destination array was not long enough. Check the destination index, length, and the array's lower bounds.</summary>
    internal static string @Arg_LongerThanDestArray => GetResourceString("Arg_LongerThanDestArray");
    /// <summary>Source array was not long enough. Check the source index, length, and the array's lower bounds.</summary>
    internal static string @Arg_LongerThanSrcArray => GetResourceString("Arg_LongerThanSrcArray");
    /// <summary>The lower bound of target array must be zero.</summary>
    internal static string @Arg_NonZeroLowerBound => GetResourceString("Arg_NonZeroLowerBound");
    /// <summary>Only single dimensional arrays are supported for the requested action.</summary>
    internal static string @Arg_RankMultiDimNotSupported => GetResourceString("Arg_RankMultiDimNotSupported");
    /// <summary>The value "{0}" is not of type "{1}" and cannot be used in this generic collection.</summary>
    internal static string @Arg_WrongType => GetResourceString("Arg_WrongType");
    /// <summary>An item with the same key has already been added. Key: {0}</summary>
    internal static string @Argument_AddingDuplicateWithKey => GetResourceString("Argument_AddingDuplicateWithKey");
    /// <summary>Target array type is not compatible with the type of items in the collection.</summary>
    internal static string @Argument_IncompatibleArrayType => GetResourceString("Argument_IncompatibleArrayType");
    /// <summary>Offset and length were out of bounds for the array or count is greater than the number of elements from index to the end of the source collection.</summary>
    internal static string @Argument_InvalidOffLen => GetResourceString("Argument_InvalidOffLen");
    /// <summary>Number was less than the array's lower bound in the first dimension.</summary>
    internal static string @ArgumentOutOfRange_ArrayLB => GetResourceString("ArgumentOutOfRange_ArrayLB");
    /// <summary>Larger than collection size.</summary>
    internal static string @ArgumentOutOfRange_BiggerThanCollection => GetResourceString("ArgumentOutOfRange_BiggerThanCollection");
    /// <summary>Count must be positive and count must refer to a location within the string/array/collection.</summary>
    internal static string @ArgumentOutOfRange_Count => GetResourceString("ArgumentOutOfRange_Count");
    /// <summary>Index was out of range. Must be non-negative and less than the size of the collection.</summary>
    internal static string @ArgumentOutOfRange_IndexMustBeLess => GetResourceString("ArgumentOutOfRange_IndexMustBeLess");
    /// <summary>Index must be within the bounds of the List.</summary>
    internal static string @ArgumentOutOfRange_ListInsert => GetResourceString("ArgumentOutOfRange_ListInsert");
    /// <summary>Non-negative number required.</summary>
    internal static string @ArgumentOutOfRange_NeedNonNegNum => GetResourceString("ArgumentOutOfRange_NeedNonNegNum");
    /// <summary>capacity was less than the current size.</summary>
    internal static string @ArgumentOutOfRange_SmallCapacity => GetResourceString("ArgumentOutOfRange_SmallCapacity");
    /// <summary>Operations that change non-concurrent collections must have exclusive access. A concurrent update was performed on this collection and corrupted its state. The collection's state is no longer correct.</summary>
    internal static string @InvalidOperation_ConcurrentOperationsNotSupported => GetResourceString("InvalidOperation_ConcurrentOperationsNotSupported");
    /// <summary>Collection was modified; enumeration operation may not execute.</summary>
    internal static string @InvalidOperation_EnumFailedVersion => GetResourceString("InvalidOperation_EnumFailedVersion");
    /// <summary>Enumeration has either not started or has already finished.</summary>
    internal static string @InvalidOperation_EnumOpCantHappen => GetResourceString("InvalidOperation_EnumOpCantHappen");
    /// <summary>Failed to compare two elements in the array.</summary>
    internal static string @InvalidOperation_IComparerFailed => GetResourceString("InvalidOperation_IComparerFailed");
    /// <summary>Mutating a key collection derived from a dictionary is not allowed.</summary>
    internal static string @NotSupported_KeyCollectionSet => GetResourceString("NotSupported_KeyCollectionSet");
    /// <summary>Mutating a value collection derived from a dictionary is not allowed.</summary>
    internal static string @NotSupported_ValueCollectionSet => GetResourceString("NotSupported_ValueCollectionSet");
    /// <summary>The specified arrays must have the same number of dimensions.</summary>
    internal static string @Rank_MustMatch => GetResourceString("Rank_MustMatch");
    /// <summary>Collection was of a fixed size.</summary>
    internal static string @NotSupported_FixedSizeCollection => GetResourceString("NotSupported_FixedSizeCollection");
    /// <summary>Object is not a array with the same number of elements as the array to compare it to.</summary>
    internal static string @ArgumentException_OtherNotArrayOfCorrectLength => GetResourceString("ArgumentException_OtherNotArrayOfCorrectLength");
    /// <summary>Unable to sort because the IComparer.Compare() method returns inconsistent results. Either a value does not compare equal to itself, or one value repeatedly compared to another value yields different results. IComparer: '{0}'.</summary>
    internal static string @Arg_BogusIComparer => GetResourceString("Arg_BogusIComparer");
    /// <summary>Cannot find the old value</summary>
    internal static string @CannotFindOldValue => GetResourceString("CannotFindOldValue");
    /// <summary>Index was out of range. Must be non-negative and less than or equal to the size of the collection.</summary>
    internal static string @ArgumentOutOfRange_IndexMustBeLessOrEqual => GetResourceString("ArgumentOutOfRange_IndexMustBeLessOrEqual");

}
