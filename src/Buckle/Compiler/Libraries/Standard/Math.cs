using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using static Buckle.Utilities.LibraryUtilities;

namespace Buckle.Libraries.Standard;

internal static partial class StandardLibrary {
    internal static ClassSymbol Math = StaticClass("Math",
        [
    /* 0 */ Constexpr("PI", BoundType.Decimal, 3.1415926535897931),
    /* 1 */ Constexpr("E", BoundType.Decimal, 2.7182818284590451),
    /* 2 */ StaticMethod("Abs", BoundType.NullableDecimal, [
                ("value", BoundType.NullableDecimal)
            ]),
    /* 3 */ StaticMethod("Abs", BoundType.Decimal, [
                ("value", BoundType.Decimal)
            ]),
    /* 4 */ StaticMethod("Abs", BoundType.NullableInt, [
                ("value", BoundType.NullableInt)
            ]),
    /* 5 */ StaticMethod("Abs", BoundType.Int, [
                ("value", BoundType.Int)
            ]),
    /* 6 */ StaticMethod("Acos", BoundType.NullableDecimal, [
                ("d", BoundType.NullableDecimal)
            ]),
    /* 7 */ StaticMethod("Acos", BoundType.Decimal, [
                ("d", BoundType.Decimal)
            ]),
    /* 8 */ StaticMethod("Acosh", BoundType.NullableDecimal, [
                ("d", BoundType.NullableDecimal)
            ]),
    /* 9 */ StaticMethod("Acosh", BoundType.Decimal, [
                ("d", BoundType.Decimal)
            ]),
   /* 10 */ StaticMethod("Asin", BoundType.NullableDecimal, [
                ("d", BoundType.NullableDecimal)
            ]),
   /* 11 */ StaticMethod("Asin", BoundType.Decimal, [
                ("d", BoundType.Decimal)
            ]),
   /* 12 */ StaticMethod("Asinh", BoundType.NullableDecimal, [
                ("d", BoundType.NullableDecimal)
            ]),
   /* 13 */ StaticMethod("Asinh", BoundType.Decimal, [
                ("d", BoundType.Decimal)
            ]),
   /* 14 */ StaticMethod("Atan", BoundType.NullableDecimal, [
                ("d", BoundType.NullableDecimal)
            ]),
   /* 15 */ StaticMethod("Atan", BoundType.Decimal, [
                ("d", BoundType.Decimal)
            ]),
   /* 16 */ StaticMethod("Atanh", BoundType.NullableDecimal, [
                ("d", BoundType.NullableDecimal)
            ]),
   /* 17 */ StaticMethod("Atanh", BoundType.Decimal, [
                ("d", BoundType.Decimal)
            ]),
   /* 18 */ StaticMethod("Ceiling", BoundType.NullableDecimal, [
                ("d", BoundType.NullableDecimal)
            ]),
   /* 19 */ StaticMethod("Ceiling", BoundType.Decimal, [
                ("d", BoundType.Decimal)
            ]),
   /* 20 */ StaticMethod("Clamp", BoundType.NullableDecimal, [
                ("value", BoundType.NullableDecimal),
                ("min", BoundType.NullableDecimal),
                ("max", BoundType.NullableDecimal)
            ]),
   /* 21 */ StaticMethod("Clamp", BoundType.Decimal, [
                ("value", BoundType.Decimal),
                ("min", BoundType.Decimal),
                ("max", BoundType.Decimal)
            ]),
   /* 22 */ StaticMethod("Clamp", BoundType.NullableInt, [
                ("value", BoundType.NullableInt),
                ("min", BoundType.NullableInt),
                ("max", BoundType.NullableInt)
            ]),
   /* 23 */ StaticMethod("Clamp", BoundType.Int, [
                ("value", BoundType.Int),
                ("min", BoundType.Int),
                ("max", BoundType.Int)
            ]),
   /* 24 */ StaticMethod("Cos", BoundType.NullableDecimal, [
                ("d", BoundType.NullableDecimal)
            ]),
   /* 25 */ StaticMethod("Cos", BoundType.Decimal, [
                ("d", BoundType.Decimal)
            ]),
   /* 26 */ StaticMethod("Cosh", BoundType.NullableDecimal, [
                ("d", BoundType.NullableDecimal)
            ]),
   /* 27 */ StaticMethod("Cosh", BoundType.Decimal, [
                ("d", BoundType.Decimal)
            ]),
   /* 28 */ StaticMethod("Exp", BoundType.NullableDecimal, [
                ("d", BoundType.NullableDecimal)
            ]),
   /* 29 */ StaticMethod("Exp", BoundType.Decimal, [
                ("d", BoundType.Decimal)
            ]),
   /* 30 */ StaticMethod("Floor", BoundType.NullableDecimal, [
                ("d", BoundType.NullableDecimal)
            ]),
   /* 31 */ StaticMethod("Floor", BoundType.Decimal, [
                ("d", BoundType.Decimal)
            ]),
   /* 32 */ StaticMethod("Lerp", BoundType.NullableDecimal, [
                ("start", BoundType.NullableDecimal),
                ("end", BoundType.NullableDecimal),
                ("rate", BoundType.NullableDecimal)
            ]),
   /* 33 */ StaticMethod("Lerp", BoundType.Decimal, [
                ("start", BoundType.Decimal),
                ("end", BoundType.Decimal),
                ("rate", BoundType.Decimal)
            ]),
   /* 34 */ StaticMethod("Log", BoundType.NullableDecimal, [
                ("d", BoundType.NullableDecimal),
                ("base", BoundType.NullableDecimal)
            ]),
   /* 35 */ StaticMethod("Log", BoundType.Decimal, [
                ("d", BoundType.Decimal),
                ("base", BoundType.Decimal)
            ]),
   /* 36 */ StaticMethod("Log", BoundType.NullableDecimal, [
                ("d", BoundType.NullableDecimal)
            ]),
   /* 37 */ StaticMethod("Log", BoundType.Decimal, [
                ("d", BoundType.Decimal)
            ]),
   /* 38 */ StaticMethod("Max", BoundType.NullableDecimal, [
                ("val1", BoundType.NullableDecimal),
                ("val2", BoundType.NullableDecimal)
            ]),
   /* 39 */ StaticMethod("Max", BoundType.Decimal, [
                ("val1", BoundType.Decimal),
                ("val2", BoundType.Decimal)
            ]),
   /* 40 */ StaticMethod("Max", BoundType.NullableInt, [
                ("val1", BoundType.NullableInt),
                ("val2", BoundType.NullableInt)
            ]),
   /* 41 */ StaticMethod("Max", BoundType.Int, [
                ("val1", BoundType.Int),
                ("val2", BoundType.Int)
            ]),
   /* 42 */ StaticMethod("Min", BoundType.NullableDecimal, [
                ("val1", BoundType.NullableDecimal),
                ("val2", BoundType.NullableDecimal)
            ]),
   /* 43 */ StaticMethod("Min", BoundType.Decimal, [
                ("val1", BoundType.Decimal),
                ("val2", BoundType.Decimal)
            ]),
   /* 44 */ StaticMethod("Min", BoundType.NullableInt, [
                ("val1", BoundType.NullableInt),
                ("val2", BoundType.NullableInt)
            ]),
   /* 45 */ StaticMethod("Min", BoundType.Int, [
                ("val1", BoundType.Int),
                ("val2", BoundType.Int)
            ]),
   /* 46 */ StaticMethod("Pow", BoundType.NullableDecimal, [
                ("x", BoundType.NullableDecimal),
                ("y", BoundType.NullableDecimal)
            ]),
   /* 47 */ StaticMethod("Pow", BoundType.Decimal, [
                ("x", BoundType.Decimal),
                ("y", BoundType.Decimal)
            ]),
   /* 48 */ StaticMethod("Round", BoundType.NullableDecimal, [
                ("value", BoundType.NullableDecimal)
            ]),
   /* 49 */ StaticMethod("Round", BoundType.Decimal, [
                ("value", BoundType.Decimal)
            ]),
   /* 50 */ StaticMethod("Sin", BoundType.NullableDecimal, [
                ("d", BoundType.NullableDecimal)
            ]),
   /* 51 */ StaticMethod("Sin", BoundType.Decimal, [
                ("d", BoundType.Decimal)
            ]),
   /* 52 */ StaticMethod("Sinh", BoundType.NullableDecimal, [
                ("d", BoundType.NullableDecimal)
            ]),
   /* 53 */ StaticMethod("Sinh", BoundType.Decimal, [
                ("d", BoundType.Decimal)
            ]),
   /* 54 */ StaticMethod("Sqrt", BoundType.NullableDecimal, [
                ("d", BoundType.NullableDecimal)
            ]),
   /* 55 */ StaticMethod("Sqrt", BoundType.Decimal, [
                ("d", BoundType.Decimal)
            ]),
   /* 56 */ StaticMethod("Tan", BoundType.NullableDecimal, [
                ("d", BoundType.NullableDecimal)
            ]),
   /* 57 */ StaticMethod("Tan", BoundType.Decimal, [
                ("d", BoundType.Decimal)
            ]),
   /* 58 */ StaticMethod("Tanh", BoundType.NullableDecimal, [
                ("d", BoundType.NullableDecimal)
            ]),
   /* 59 */ StaticMethod("Tanh", BoundType.Decimal, [
                ("d", BoundType.Decimal)
            ]),
   /* 60 */ StaticMethod("Truncate", BoundType.NullableDecimal, [
                ("value", BoundType.NullableDecimal)
            ]),
   /* 61 */ StaticMethod("Truncate", BoundType.Decimal, [
                ("value", BoundType.Decimal)
            ]),
        ]
    );
}
