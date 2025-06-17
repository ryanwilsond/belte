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

    public static object[] Sort(object[] array) {
        Array.Sort(array);
        return array;
    }

    public static long? Length(object[] array) {
        return array?.LongLength;
    }

    public static long TimeNow() {
        return DateTime.Now.Ticks;
    }

    public static void TimeSleep(long ms) {
        Thread.Sleep((int)ms);
    }
}
