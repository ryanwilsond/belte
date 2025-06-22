
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

    public static double? Cos(double? a) {
        if (a is null)
            return null;

        return System.Math.Cos(a.Value);
    }

    public static double? Sin(double? a) {
        if (a is null)
            return null;

        return System.Math.Sin(a.Value);
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
}
