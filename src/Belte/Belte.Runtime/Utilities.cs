using System;
using System.Threading;

namespace Belte.Runtime;

public static class Utilities {
    public static int GetHashCode(object o) {
        return o.GetHashCode();
    }

    public static int HashCodeCombine(int hash1, int hash2) {
        return HashCode.Combine(hash1, hash2);
    }

    public static int HashCodeCombine(int hash1, int hash2, int hash3) {
        return HashCode.Combine(hash1, hash2, hash3);
    }

    public static int HashCodeCombine(int hash1, int hash2, int hash3, int hash4) {
        return HashCode.Combine(hash1, hash2, hash3, hash4);
    }

    public static int HashCodeCombine(int hash1, int hash2, int hash3, int hash4, int hash5) {
        return HashCode.Combine(hash1, hash2, hash3, hash4, hash5);
    }

    public static int HashCodeCombine(int hash1, int hash2, int hash3, int hash4, int hash5, int hash6) {
        return HashCode.Combine(hash1, hash2, hash3, hash4, hash5, hash6);
    }

    public static int HashCodeCombine(int hash1, int hash2, int hash3, int hash4, int hash5, int hash6, int hash7) {
        return HashCode.Combine(hash1, hash2, hash3, hash4, hash5, hash6, hash7);
    }

    public static int HashCodeCombine(int hash1, int hash2, int hash3, int hash4, int hash5, int hash6, int hash7, int hash8) {
        return HashCode.Combine(hash1, hash2, hash3, hash4, hash5, hash6, hash7, hash8);
    }

    public static string GetTypeName(object o) {
        return o.GetType().Name;
    }

    public static Type AnyGetType(object o) {
        return o.GetType();
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

    public static long StringIndexOf(string str, char chr) {
        return str.IndexOf(chr);
    }

    public static string StringPadLeft(string str, char chr, long width) {
        return str.PadLeft((int)width, chr);
    }

    public static string StringPadRight(string str, char chr, long width) {
        return str.PadRight((int)width, chr);
    }

    public static string StringReplace(string str, string search, string replacement) {
        return str.Replace(search, replacement);
    }

    public static string StringTrim(string str) {
        return str.Trim();
    }

    public static string StringTrim(string str, char[] trimChars) {
        return str.Trim(trimChars);
    }

    public static string StringTrimStart(string str) {
        return str.TrimStart();
    }

    public static string StringTrimStart(string str, char[] trimChars) {
        return str.TrimStart(trimChars);
    }

    public static string StringTrimEnd(string str) {
        return str.TrimEnd();
    }

    public static string StringTrimEnd(string str, char[] trimChars) {
        return str.TrimEnd(trimChars);
    }

    public static void CreateDirectory(string path) {
        System.IO.Directory.CreateDirectory(path);
    }

    public static void DeleteDirectory(string path) {
        System.IO.Directory.Delete(path, recursive: true);
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

    public static double? DecimalParse(string text) {
        if (text is null)
            return null;

        if (double.TryParse(text, out var result))
            return result;

        return null;
    }

    public static string IntToString(long num, string format) {
        return num.ToString(format);
    }

    public static string DecimalToString(double num, string format) {
        return num.ToString(format);
    }

    public static string[] Split(string text, string separator) {
        return text.Split(separator);
    }

    public unsafe static byte* CreateLPCSTR(string str) {
        return (byte*)System.Runtime.InteropServices.Marshal.StringToHGlobalAnsi(str);
    }

    public unsafe static byte* CreateLPCSTR_UTF(string str) {
        var utf8 = System.Text.Encoding.UTF8.GetBytes(str);
        var ptr = (byte*)System.Runtime.InteropServices.Marshal.AllocHGlobal(utf8.Length + 1);
        System.Runtime.InteropServices.Marshal.Copy(utf8, 0, (nint)ptr, utf8.Length);
        ptr[utf8.Length] = 0;
        return ptr;
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
