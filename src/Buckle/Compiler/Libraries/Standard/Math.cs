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
            StaticMethod("Abs", BoundType.NullableInt, [
                ("value", BoundType.NullableInt)
            ]),
            StaticMethod("Acos", BoundType.NullableDecimal, [
                ("d", BoundType.NullableDecimal)
            ]),
            StaticMethod("Acosh", BoundType.NullableDecimal, [
                ("d", BoundType.NullableDecimal)
            ]),
            StaticMethod("Asin", BoundType.NullableDecimal, [
                ("d", BoundType.NullableDecimal)
            ]),
            StaticMethod("Asinh", BoundType.NullableDecimal, [
                ("d", BoundType.NullableDecimal)
            ]),
            StaticMethod("Atan", BoundType.NullableDecimal, [
                ("d", BoundType.NullableDecimal)
            ]),
            StaticMethod("Atanh", BoundType.NullableDecimal, [
                ("d", BoundType.NullableDecimal)
            ]),
            StaticMethod("Ceiling", BoundType.NullableDecimal, [
                ("d", BoundType.NullableDecimal)
            ]),
            StaticMethod("Clamp", BoundType.NullableDecimal, [
                ("value", BoundType.NullableDecimal),
                ("min", BoundType.NullableDecimal),
                ("max", BoundType.NullableDecimal)
            ]),
            StaticMethod("Clamp", BoundType.NullableInt, [
                ("value", BoundType.NullableInt),
                ("min", BoundType.NullableInt),
                ("max", BoundType.NullableInt)
            ]),
            StaticMethod("Cos", BoundType.NullableDecimal, [
                ("d", BoundType.NullableDecimal)
            ]),
            StaticMethod("Cosh", BoundType.NullableDecimal, [
                ("d", BoundType.NullableDecimal)
            ]),
            StaticMethod("Exp", BoundType.NullableDecimal, [
                ("d", BoundType.NullableDecimal)
            ]),
            StaticMethod("Floor", BoundType.NullableDecimal, [
                ("d", BoundType.NullableDecimal)
            ]),
            StaticMethod("Lerp", BoundType.NullableDecimal, [
                ("start", BoundType.NullableDecimal),
                ("end", BoundType.NullableDecimal),
                ("rate", BoundType.NullableDecimal)
            ]),
            StaticMethod("Log", BoundType.NullableDecimal, [
                ("d", BoundType.NullableDecimal),
                ("base", BoundType.NullableDecimal)
            ]),
            StaticMethod("Log", BoundType.NullableDecimal, [
                ("d", BoundType.NullableDecimal)
            ]),
            StaticMethod("Max", BoundType.NullableDecimal, [
                ("val1", BoundType.NullableDecimal),
                ("val2", BoundType.NullableDecimal)
            ]),
            StaticMethod("Max", BoundType.NullableInt, [
                ("val1", BoundType.NullableInt),
                ("val2", BoundType.NullableInt)
            ]),
            StaticMethod("Min", BoundType.NullableDecimal, [
                ("val1", BoundType.NullableDecimal),
                ("val2", BoundType.NullableDecimal)
            ]),
            StaticMethod("Min", BoundType.NullableInt, [
                ("val1", BoundType.NullableInt),
                ("val2", BoundType.NullableInt)
            ]),
            StaticMethod("Pow", BoundType.NullableDecimal, [
                ("x", BoundType.NullableDecimal),
                ("y", BoundType.NullableDecimal)
            ]),
            StaticMethod("Round", BoundType.NullableDecimal, [
                ("value", BoundType.NullableDecimal)
            ]),
            StaticMethod("Sin", BoundType.NullableDecimal, [
                ("d", BoundType.NullableDecimal)
            ]),
            StaticMethod("Sinh", BoundType.NullableDecimal, [
                ("d", BoundType.NullableDecimal)
            ]),
            StaticMethod("Sqrt", BoundType.NullableDecimal, [
                ("d", BoundType.NullableDecimal)
            ]),
            StaticMethod("Tan", BoundType.NullableDecimal, [
                ("d", BoundType.NullableDecimal)
            ]),
            StaticMethod("Tanh", BoundType.NullableDecimal, [
                ("d", BoundType.NullableDecimal)
            ]),
            StaticMethod("Truncate", BoundType.NullableDecimal, [
                ("value", BoundType.NullableDecimal)
            ]),
        ]
    );
}
