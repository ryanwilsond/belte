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

    public static T AssertNull<T>(T value) {
        if (value is null)
            throw new NullReferenceException();

        return value;
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

    public static long StringLength(string str) {
        return str.Length;
    }

    public static long TimeNow() {
        return DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
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

    public static bool IsDigit(char? chr) {
        return chr.HasValue && char.IsDigit(chr.Value);
    }

    public static string Substring(string text, long? start, long? length) {
        if (text is null)
            return null;

        if (length is null)
            return text.Substring(start.HasValue ? unchecked((int)start.Value) : 0);

        return text.Substring(start.HasValue ? unchecked((int)start.Value) : 0, unchecked((int)length.Value));
    }

    public static bool IsNullOrWhiteSpace(char? chr) {
        return !chr.HasValue || char.IsWhiteSpace(chr.Value);
    }

    public static long? IntParse(string text) {
        if (text is null)
            return null;

        if (long.TryParse(text, out var result))
            return result;

        return null;
    }

    public static string[] Split(string text, string separator) {
        return text.Split(separator);
    }

    public unsafe static byte* CreateLPCSTR(string str) {
        return (byte*)System.Runtime.InteropServices.Marshal.StringToHGlobalAnsi(str);
    }

    public unsafe static char* CreateLPCWSTR(string str) {
        return (char*)System.Runtime.InteropServices.Marshal.StringToHGlobalUni(str);
    }

    public unsafe static void FreeLPCSTR(byte* str) {
        System.Runtime.InteropServices.Marshal.FreeHGlobal((IntPtr)str);
    }

    public unsafe static void FreeLPCWSTR(char* str) {
        System.Runtime.InteropServices.Marshal.FreeHGlobal((IntPtr)str);
    }

    public unsafe static string ReadLPCSTR(byte* ptr) {
        return System.Runtime.InteropServices.Marshal.PtrToStringAnsi((IntPtr)ptr);
    }

    public unsafe static string ReadLPCWSTR(char* ptr) {
        return System.Runtime.InteropServices.Marshal.PtrToStringUni((IntPtr)ptr);
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
}
