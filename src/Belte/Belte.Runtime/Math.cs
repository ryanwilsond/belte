
namespace Belte.Runtime;

public static class Math {
    public static double? Lerp(double? a, double? b, double? c) {
        return a + c * (b - a);
    }

    public static double Lerp(double a, double b, double c) {
        return a + c * (b - a);
    }

    public static double? Clamp(double? a, double? b, double? c) {
        if (a is null || b is null || c is null)
            return null;

        return System.Math.Clamp(a.Value, b.Value, c.Value);
    }

    public static float? Clamp(float? a, float? b, float? c) {
        if (a is null || b is null || c is null)
            return null;

        return System.Math.Clamp(a.Value, b.Value, c.Value);
    }

    public static long? Clamp(long? a, long? b, long? c) {
        if (a is null || b is null || c is null)
            return null;

        return System.Math.Clamp(a.Value, b.Value, c.Value);
    }

    public static ulong? Clamp(ulong? a, ulong? b, ulong? c) {
        if (a is null || b is null || c is null)
            return null;

        return System.Math.Clamp(a.Value, b.Value, c.Value);
    }

    public static int? Clamp(int? a, int? b, int? c) {
        if (a is null || b is null || c is null)
            return null;

        return System.Math.Clamp(a.Value, b.Value, c.Value);
    }

    public static uint? Clamp(uint? a, uint? b, uint? c) {
        if (a is null || b is null || c is null)
            return null;

        return System.Math.Clamp(a.Value, b.Value, c.Value);
    }

    public static short? Clamp(short? a, short? b, short? c) {
        if (a is null || b is null || c is null)
            return null;

        return System.Math.Clamp(a.Value, b.Value, c.Value);
    }

    public static ushort? Clamp(ushort? a, ushort? b, ushort? c) {
        if (a is null || b is null || c is null)
            return null;

        return System.Math.Clamp(a.Value, b.Value, c.Value);
    }

    public static sbyte? Clamp(sbyte? a, sbyte? b, sbyte? c) {
        if (a is null || b is null || c is null)
            return null;

        return System.Math.Clamp(a.Value, b.Value, c.Value);
    }

    public static byte? Clamp(byte? a, byte? b, byte? c) {
        if (a is null || b is null || c is null)
            return null;

        return System.Math.Clamp(a.Value, b.Value, c.Value);
    }

    public static char? Clamp(char? a, char? b, char? c) {
        if (a is null || b is null || c is null)
            return null;

        return (char?)System.Math.Clamp(a.Value, b.Value, c.Value);
    }

    public static char Clamp(char a, char b, char c) {
        return (char)System.Math.Clamp(a, b, c);
    }

    public static double? Acos(double? a) {
        if (a is null)
            return null;

        return System.Math.Acos(a.Value);
    }

    public static double? Acosh(double? a) {
        if (a is null)
            return null;

        return System.Math.Acosh(a.Value);
    }

    public static double? Cos(double? a) {
        if (a is null)
            return null;

        return System.Math.Cos(a.Value);
    }

    public static double? Cosh(double? a) {
        if (a is null)
            return null;

        return System.Math.Cosh(a.Value);
    }

    public static double? Asin(double? a) {
        if (a is null)
            return null;

        return System.Math.Asin(a.Value);
    }

    public static double? Asinh(double? a) {
        if (a is null)
            return null;

        return System.Math.Acosh(a.Value);
    }

    public static double? Sin(double? a) {
        if (a is null)
            return null;

        return System.Math.Sin(a.Value);
    }

    public static double? Sinh(double? a) {
        if (a is null)
            return null;

        return System.Math.Sinh(a.Value);
    }

    public static double? Atan(double? a) {
        if (a is null)
            return null;

        return System.Math.Atan(a.Value);
    }

    public static double? Atanh(double? a) {
        if (a is null)
            return null;

        return System.Math.Atanh(a.Value);
    }

    public static double? Tan(double? a) {
        if (a is null)
            return null;

        return System.Math.Tan(a.Value);
    }

    public static double? Tanh(double? a) {
        if (a is null)
            return null;

        return System.Math.Tanh(a.Value);
    }

    public static double? Pow(double? a, double? b) {
        if (a is null || b is null)
            return null;

        return System.Math.Pow(a.Value, b.Value);
    }

    public static long? Pow(long? a, long? b) {
        if (a is null || b is null)
            return null;

        return (long?)System.Math.Pow(a.Value, b.Value);
    }

    public static long Pow(long a, long b) {
        return (long)System.Math.Pow(a, b);
    }

    public static double? Abs(double? a) {
        if (a is null)
            return null;

        return System.Math.Abs(a.Value);
    }

    public static long? Abs(long? a) {
        if (a is null)
            return null;

        return System.Math.Abs(a.Value);
    }

    public static double? Round(double? a) {
        if (a is null)
            return null;

        return System.Math.Round(a.Value);
    }

    public static double? Min(double? a, double? b) {
        if (a is null || b is null)
            return null;

        return System.Math.Min(a.Value, b.Value);
    }

    public static float? Min(float? a, float? b) {
        if (a is null || b is null)
            return null;

        return System.Math.Min(a.Value, b.Value);
    }

    public static long? Min(long? a, long? b) {
        if (a is null || b is null)
            return null;

        return System.Math.Min(a.Value, b.Value);
    }

    public static ulong? Min(ulong? a, ulong? b) {
        if (a is null || b is null)
            return null;

        return System.Math.Min(a.Value, b.Value);
    }

    public static int? Min(int? a, int? b) {
        if (a is null || b is null)
            return null;

        return System.Math.Min(a.Value, b.Value);
    }

    public static uint? Min(uint? a, uint? b) {
        if (a is null || b is null)
            return null;

        return System.Math.Min(a.Value, b.Value);
    }

    public static double? Max(double? a, double? b) {
        if (a is null || b is null)
            return null;

        return System.Math.Max(a.Value, b.Value);
    }

    public static float? Max(float? a, float? b) {
        if (a is null || b is null)
            return null;

        return System.Math.Max(a.Value, b.Value);
    }

    public static long? Max(long? a, long? b) {
        if (a is null || b is null)
            return null;

        return System.Math.Max(a.Value, b.Value);
    }

    public static ulong? Max(ulong? a, ulong? b) {
        if (a is null || b is null)
            return null;

        return System.Math.Max(a.Value, b.Value);
    }

    public static int? Max(int? a, int? b) {
        if (a is null || b is null)
            return null;

        return System.Math.Max(a.Value, b.Value);
    }

    public static uint? Max(uint? a, uint? b) {
        if (a is null || b is null)
            return null;

        return System.Math.Max(a.Value, b.Value);
    }

    public static double? Floor(double? a) {
        if (a is null)
            return null;

        return System.Math.Floor(a.Value);
    }

    public static double? Ceiling(double? a) {
        if (a is null)
            return null;

        return System.Math.Ceiling(a.Value);
    }

    public static long? Sign(double? a) {
        if (a is null)
            return null;

        return System.Math.Sign(a.Value);
    }

    public static long? Sign(long? a) {
        if (a is null)
            return null;

        return System.Math.Sign(a.Value);
    }

    public static double? Exp(double? a) {
        if (a is null)
            return null;

        return System.Math.Exp(a.Value);
    }

    public static double? Log(double? a) {
        if (a is null)
            return null;

        return System.Math.Log(a.Value);
    }

    public static double? Log(double? a, double? b) {
        if (a is null)
            return null;

        return System.Math.Log(a.Value, b.Value);
    }

    public static double? Sqrt(double? a) {
        if (a is null)
            return null;

        return System.Math.Sqrt(a.Value);
    }

    public static double? Truncate(double? a) {
        if (a is null)
            return null;

        return System.Math.Truncate(a.Value);
    }

    public static double? DegToRad(double? a) {
        if (a is null)
            return null;

        return double.DegreesToRadians(a.Value);
    }

    public static double? RadToDeg(double? a) {
        if (a is null)
            return null;

        return double.RadiansToDegrees(a.Value);
    }
}
