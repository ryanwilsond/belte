using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using static Buckle.Utilities.LibraryUtilities;

namespace Buckle.Libraries.Standard;

internal static partial class StandardLibrary {
    internal static ClassSymbol Math = StaticClass("Math",
        [
            Constexpr("PI", BoundType.Decimal, 3.1415926535897931),
            Constexpr("E", BoundType.Decimal, 2.7182818284590451),
            StaticMethod("Abs", BoundType.Decimal, [
                ("value", BoundType.Decimal)
            ]),
            StaticMethod("Abs", BoundType.Int, [
                ("value", BoundType.Int)
            ]),
            StaticMethod("Acos", BoundType.Decimal, [
                ("d", BoundType.Decimal)
            ]),
            StaticMethod("Acosh", BoundType.Decimal, [
                ("d", BoundType.Decimal)
            ]),
            StaticMethod("Asin", BoundType.Decimal, [
                ("d", BoundType.Decimal)
            ]),
            StaticMethod("Asinh", BoundType.Decimal, [
                ("d", BoundType.Decimal)
            ]),
            StaticMethod("Atan", BoundType.Decimal, [
                ("d", BoundType.Decimal)
            ]),
            StaticMethod("Atanh", BoundType.Decimal, [
                ("d", BoundType.Decimal)
            ]),
            StaticMethod("Ceiling", BoundType.Decimal, [
                ("d", BoundType.Decimal)
            ]),
            StaticMethod("Clamp", BoundType.Decimal, [
                ("value", BoundType.Decimal),
                ("min", BoundType.Decimal),
                ("max", BoundType.Decimal)
            ]),
            StaticMethod("Clamp", BoundType.Int, [
                ("value", BoundType.Int),
                ("min", BoundType.Int),
                ("max", BoundType.Int)
            ]),
            StaticMethod("Cos", BoundType.Decimal, [
                ("d", BoundType.Decimal)
            ]),
            StaticMethod("Cosh", BoundType.Decimal, [
                ("d", BoundType.Decimal)
            ]),
            StaticMethod("Exp", BoundType.Decimal, [
                ("d", BoundType.Decimal)
            ]),
            StaticMethod("Floor", BoundType.Decimal, [
                ("d", BoundType.Decimal)
            ]),
            StaticMethod("Lerp", BoundType.Decimal, [
                ("start", BoundType.Decimal),
                ("end", BoundType.Decimal),
                ("rate", BoundType.Decimal)
            ]),
            StaticMethod("Log", BoundType.Decimal, [
                ("d", BoundType.Decimal),
                ("base", BoundType.Decimal)
            ]),
            StaticMethod("Log", BoundType.Decimal, [
                ("d", BoundType.Decimal)
            ]),
            StaticMethod("Max", BoundType.Decimal, [
                ("val1", BoundType.Decimal),
                ("val2", BoundType.Decimal)
            ]),
            StaticMethod("Max", BoundType.Int, [
                ("val1", BoundType.Int),
                ("val2", BoundType.Int)
            ]),
            StaticMethod("Min", BoundType.Decimal, [
                ("val1", BoundType.Decimal),
                ("val2", BoundType.Decimal)
            ]),
            StaticMethod("Min", BoundType.Int, [
                ("val1", BoundType.Int),
                ("val2", BoundType.Int)
            ]),
            StaticMethod("Pow", BoundType.Decimal, [
                ("x", BoundType.Decimal),
                ("y", BoundType.Decimal)
            ]),
            StaticMethod("Round", BoundType.Decimal, [
                ("value", BoundType.Decimal)
            ]),
            StaticMethod("Sin", BoundType.Decimal, [
                ("d", BoundType.Decimal)
            ]),
            StaticMethod("Sinh", BoundType.Decimal, [
                ("d", BoundType.Decimal)
            ]),
            StaticMethod("Sqrt", BoundType.Decimal, [
                ("d", BoundType.Decimal)
            ]),
            StaticMethod("Tan", BoundType.Decimal, [
                ("d", BoundType.Decimal)
            ]),
            StaticMethod("Tanh", BoundType.Decimal, [
                ("d", BoundType.Decimal)
            ]),
            StaticMethod("Truncate", BoundType.Decimal, [
                ("value", BoundType.Decimal)
            ]),
        ]
    );
}
