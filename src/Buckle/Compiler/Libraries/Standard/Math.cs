using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using static Buckle.Utilities.LibraryUtilities;

namespace Buckle.Libraries.Standard;

internal static partial class StandardLibrary {
    internal static ClassSymbol Math = StaticClass("Math",
        [
            Constexpr("PI", BoundType.Decimal, 3.1415926535897931),
            Constexpr("E", BoundType.Decimal, 2.7182818284590451),
            StaticMethod("Abs", BoundType.NullableDecimal, [
                ("value", BoundType.NullableDecimal)
            ]),
            StaticMethod("Abs", BoundType.Decimal, [
                ("value", BoundType.Decimal)
            ]),
            StaticMethod("Abs", BoundType.NullableInt, [
                ("value", BoundType.NullableInt)
            ]),
            StaticMethod("Abs", BoundType.Int, [
                ("value", BoundType.Int)
            ]),
            StaticMethod("Acos", BoundType.NullableDecimal, [
                ("d", BoundType.NullableDecimal)
            ]),
            StaticMethod("Acos", BoundType.Decimal, [
                ("d", BoundType.Decimal)
            ]),
            StaticMethod("Acosh", BoundType.NullableDecimal, [
                ("d", BoundType.NullableDecimal)
            ]),
            StaticMethod("Acosh", BoundType.Decimal, [
                ("d", BoundType.Decimal)
            ]),
            StaticMethod("Asin", BoundType.NullableDecimal, [
                ("d", BoundType.NullableDecimal)
            ]),
            StaticMethod("Asin", BoundType.Decimal, [
                ("d", BoundType.Decimal)
            ]),
            StaticMethod("Asinh", BoundType.NullableDecimal, [
                ("d", BoundType.NullableDecimal)
            ]),
            StaticMethod("Asinh", BoundType.Decimal, [
                ("d", BoundType.Decimal)
            ]),
            StaticMethod("Atan", BoundType.NullableDecimal, [
                ("d", BoundType.NullableDecimal)
            ]),
            StaticMethod("Atan", BoundType.Decimal, [
                ("d", BoundType.Decimal)
            ]),
            StaticMethod("Atanh", BoundType.NullableDecimal, [
                ("d", BoundType.NullableDecimal)
            ]),
            StaticMethod("Atanh", BoundType.Decimal, [
                ("d", BoundType.Decimal)
            ]),
            StaticMethod("Ceiling", BoundType.NullableDecimal, [
                ("d", BoundType.NullableDecimal)
            ]),
            StaticMethod("Ceiling", BoundType.Decimal, [
                ("d", BoundType.Decimal)
            ]),
            StaticMethod("Clamp", BoundType.NullableDecimal, [
                ("value", BoundType.NullableDecimal),
                ("min", BoundType.NullableDecimal),
                ("max", BoundType.NullableDecimal)
            ]),
            StaticMethod("Clamp", BoundType.Decimal, [
                ("value", BoundType.Decimal),
                ("min", BoundType.Decimal),
                ("max", BoundType.Decimal)
            ]),
            StaticMethod("Clamp", BoundType.NullableInt, [
                ("value", BoundType.NullableInt),
                ("min", BoundType.NullableInt),
                ("max", BoundType.NullableInt)
            ]),
            StaticMethod("Clamp", BoundType.Int, [
                ("value", BoundType.Int),
                ("min", BoundType.Int),
                ("max", BoundType.Int)
            ]),
            StaticMethod("Cos", BoundType.NullableDecimal, [
                ("d", BoundType.NullableDecimal)
            ]),
            StaticMethod("Cos", BoundType.Decimal, [
                ("d", BoundType.Decimal)
            ]),
            StaticMethod("Cosh", BoundType.NullableDecimal, [
                ("d", BoundType.NullableDecimal)
            ]),
            StaticMethod("Cosh", BoundType.Decimal, [
                ("d", BoundType.Decimal)
            ]),
            StaticMethod("Exp", BoundType.NullableDecimal, [
                ("d", BoundType.NullableDecimal)
            ]),
            StaticMethod("Exp", BoundType.Decimal, [
                ("d", BoundType.Decimal)
            ]),
            StaticMethod("Floor", BoundType.NullableDecimal, [
                ("d", BoundType.NullableDecimal)
            ]),
            StaticMethod("Floor", BoundType.Decimal, [
                ("d", BoundType.Decimal)
            ]),
            StaticMethod("Lerp", BoundType.NullableDecimal, [
                ("start", BoundType.NullableDecimal),
                ("end", BoundType.NullableDecimal),
                ("rate", BoundType.NullableDecimal)
            ]),
            StaticMethod("Lerp", BoundType.Decimal, [
                ("start", BoundType.Decimal),
                ("end", BoundType.Decimal),
                ("rate", BoundType.Decimal)
            ]),
            StaticMethod("Log", BoundType.NullableDecimal, [
                ("d", BoundType.NullableDecimal),
                ("base", BoundType.NullableDecimal)
            ]),
            StaticMethod("Log", BoundType.Decimal, [
                ("d", BoundType.Decimal),
                ("base", BoundType.Decimal)
            ]),
            StaticMethod("Log", BoundType.NullableDecimal, [
                ("d", BoundType.NullableDecimal)
            ]),
            StaticMethod("Log", BoundType.Decimal, [
                ("d", BoundType.Decimal)
            ]),
            StaticMethod("Max", BoundType.NullableDecimal, [
                ("val1", BoundType.NullableDecimal),
                ("val2", BoundType.NullableDecimal)
            ]),
            StaticMethod("Max", BoundType.Decimal, [
                ("val1", BoundType.Decimal),
                ("val2", BoundType.Decimal)
            ]),
            StaticMethod("Max", BoundType.NullableInt, [
                ("val1", BoundType.NullableInt),
                ("val2", BoundType.NullableInt)
            ]),
            StaticMethod("Max", BoundType.Int, [
                ("val1", BoundType.Int),
                ("val2", BoundType.Int)
            ]),
            StaticMethod("Min", BoundType.NullableDecimal, [
                ("val1", BoundType.NullableDecimal),
                ("val2", BoundType.NullableDecimal)
            ]),
            StaticMethod("Min", BoundType.Decimal, [
                ("val1", BoundType.Decimal),
                ("val2", BoundType.Decimal)
            ]),
            StaticMethod("Min", BoundType.NullableInt, [
                ("val1", BoundType.NullableInt),
                ("val2", BoundType.NullableInt)
            ]),
            StaticMethod("Min", BoundType.Int, [
                ("val1", BoundType.Int),
                ("val2", BoundType.Int)
            ]),
            StaticMethod("Pow", BoundType.NullableDecimal, [
                ("x", BoundType.NullableDecimal),
                ("y", BoundType.NullableDecimal)
            ]),
            StaticMethod("Pow", BoundType.Decimal, [
                ("x", BoundType.Decimal),
                ("y", BoundType.Decimal)
            ]),
            StaticMethod("Round", BoundType.NullableDecimal, [
                ("value", BoundType.NullableDecimal)
            ]),
            StaticMethod("Round", BoundType.Decimal, [
                ("value", BoundType.Decimal)
            ]),
            StaticMethod("Sin", BoundType.NullableDecimal, [
                ("d", BoundType.NullableDecimal)
            ]),
            StaticMethod("Sin", BoundType.Decimal, [
                ("d", BoundType.Decimal)
            ]),
            StaticMethod("Sinh", BoundType.NullableDecimal, [
                ("d", BoundType.NullableDecimal)
            ]),
            StaticMethod("Sinh", BoundType.Decimal, [
                ("d", BoundType.Decimal)
            ]),
            StaticMethod("Sqrt", BoundType.NullableDecimal, [
                ("d", BoundType.NullableDecimal)
            ]),
            StaticMethod("Sqrt", BoundType.Decimal, [
                ("d", BoundType.Decimal)
            ]),
            StaticMethod("Tan", BoundType.NullableDecimal, [
                ("d", BoundType.NullableDecimal)
            ]),
            StaticMethod("Tan", BoundType.Decimal, [
                ("d", BoundType.Decimal)
            ]),
            StaticMethod("Tanh", BoundType.NullableDecimal, [
                ("d", BoundType.NullableDecimal)
            ]),
            StaticMethod("Tanh", BoundType.Decimal, [
                ("d", BoundType.Decimal)
            ]),
            StaticMethod("Truncate", BoundType.NullableDecimal, [
                ("value", BoundType.NullableDecimal)
            ]),
            StaticMethod("Truncate", BoundType.Decimal, [
                ("value", BoundType.Decimal)
            ]),
        ]
    );
}
