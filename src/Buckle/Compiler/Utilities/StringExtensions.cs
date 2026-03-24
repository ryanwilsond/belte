
namespace Buckle.Utilities;

internal static class StringExtensions {
    internal static bool IsValidUnicodeString(this string str) {
        var i = 0;

        while (i < str.Length) {
            var c = str[i++];

            if (char.IsHighSurrogate(c)) {
                if (i < str.Length && char.IsLowSurrogate(str[i]))
                    i++;
                else
                    return false;
            } else if (char.IsLowSurrogate(c)) {
                return false;
            }
        }

        return true;
    }
}
