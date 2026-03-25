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

    public static void Sort<T>(T array) {
        if (array is null)
            return;

        Array.Sort((Array)(object)array);
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

    public unsafe static char* CreateCharPtrString(string str) {
        return (char*)System.Runtime.InteropServices.Marshal.StringToHGlobalAnsi(str);
    }

    public unsafe static void FreeCharPtrString(char* str) {
        System.Runtime.InteropServices.Marshal.FreeHGlobal((IntPtr)str);
    }

    public unsafe static void* GetGCPtr(object obj) {
        var handle = System.Runtime.InteropServices.GCHandle.Alloc(obj, System.Runtime.InteropServices.GCHandleType.Normal);
        return (void*)System.Runtime.InteropServices.GCHandle.ToIntPtr(handle);
    }

    public unsafe static void FreeGCHandle(void* ptr) {
        var handle = System.Runtime.InteropServices.GCHandle.FromIntPtr((IntPtr)ptr);
        handle.Free();
    }

    public unsafe static object GetObject(void* ptr) {
        var handle = System.Runtime.InteropServices.GCHandle.FromIntPtr((IntPtr)ptr);
        return handle.Target;
    }

    public unsafe static string ReadLPCSTR(void* ptr) {
        var cPtr = (char*)ptr;
        var len = 0;

        while (cPtr[len] != '\0')
            len++;

        return new string(cPtr, 0, len);
    }
}
