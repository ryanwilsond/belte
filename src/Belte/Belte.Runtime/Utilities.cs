using System;
using System.Threading;

namespace Belte.Runtime;

public static class Utilities {
    public static int GetHashCode(object o) {
        return o.GetHashCode();
    }

    public static string GetTypeName(object o) {
        return o.GetType().Name;
    }

    public static T Sort<T>(T array) {
        if (array is null)
            return array;

        Array.Sort((Array)(object)array);
        return array;
    }

    public static long Length<T>(T array) {
        if (array is null)
            return 0;

        return ((Array)(object)array).LongLength;
    }

    public static long TimeNow() {
        return DateTime.Now.Ticks;
    }

    public static void TimeSleep(long ms) {
        Thread.Sleep((int)ms);
    }

    public static long? Ascii(string chr) {
        return char.TryParse(chr, out var result) ? result : null;
    }

    public static string Char(long ascii) {
        return ((char)ascii).ToString();
    }

    public static string[] Split(string text, string separator) {
        return text.Split(separator);
    }
}
