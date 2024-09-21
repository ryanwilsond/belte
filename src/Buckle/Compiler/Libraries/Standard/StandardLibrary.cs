using System;
using System.Collections.Generic;
using Buckle.CodeAnalysis;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using static Buckle.CodeAnalysis.Syntax.SyntaxFactory;
using static Buckle.Utilities.LibraryUtilities;

namespace Buckle.Libraries.Standard;

internal static partial class StandardLibrary {
    internal static ClassSymbol Object = Class("Object",
        [
    /* 0 */ Constructor([], Accessibility.Protected),
    /* 1 */ Method(
                "ToString",
                BoundType.NullableString,
                [],
                DeclarationModifiers.Virtual,
                Accessibility.Public,
                MethodDeclaration(
                    null,
                    TokenList(Token(SyntaxKind.VirtualKeyword)),
                    IdentifierName("string"),
                    Identifier("ToString"),
                    TemplateParameterList(),
                    ParameterList(
                        Token(SyntaxKind.OpenParenToken),
                        SeparatedList<ParameterSyntax>(),
                        Token(SyntaxKind.CloseParenToken)
                    ),
                    ConstraintClauseList(),
                    Block(Return(Literal(""))),
                    Token(SyntaxKind.SemicolonToken)
                )
            ),
        ]
    );

    internal static ClassSymbol Console = StaticClass("Console",
        [
    /* 0 */ StaticClass("Color", [
                Constexpr("Black", BoundType.Int, 0),
                Constexpr("DarkBlue", BoundType.Int, 1),
                Constexpr("DarkGreen", BoundType.Int, 2),
                Constexpr("DarkCyan", BoundType.Int, 3),
                Constexpr("DarkRed", BoundType.Int, 4),
                Constexpr("DarkMagenta", BoundType.Int, 5),
                Constexpr("DarkYellow", BoundType.Int, 6),
                Constexpr("Gray", BoundType.Int, 7),
                Constexpr("DarkGray", BoundType.Int, 8),
                Constexpr("Blue", BoundType.Int, 9),
                Constexpr("Green", BoundType.Int, 10),
                Constexpr("Cyan", BoundType.Int, 11),
                Constexpr("Red", BoundType.Int, 12),
                Constexpr("Magenta", BoundType.Int, 13),
                Constexpr("Yellow", BoundType.Int, 14),
                Constexpr("White", BoundType.Int, 15)
            ]),
    /* 1 */ StaticMethod("GetWidth", BoundType.Int, []),
    /* 2 */ StaticMethod("GetHeight", BoundType.Int, []),
    /* 3 */ StaticMethod("Input", BoundType.String, []),
    /* 4 */ StaticMethod("PrintLine", BoundType.Void, [
                ("message", BoundType.NullableString)
            ]),
    /* 5 */ StaticMethod("PrintLine", BoundType.Void, [
                ("value", BoundType.NullableAny)
            ]),
    /* 6 */ StaticMethod("PrintLine", BoundType.Void, [
                ("value", new BoundType(Object, isNullable: true))
            ]),
    /* 7 */ StaticMethod("PrintLine", BoundType.Void, []),
    /* 8 */ StaticMethod("Print", BoundType.Void, [
                ("message", BoundType.NullableString)
            ]),
    /* 9 */ StaticMethod("Print", BoundType.Void, [
                ("value", BoundType.NullableAny)
            ]),
   /* 10 */ StaticMethod("Print", BoundType.Void, [
                ("value", new BoundType(Object, isNullable: true))
            ]),
   /* 11 */ StaticMethod("ResetColor", BoundType.Void, []),
   /* 12 */ StaticMethod("SetForegroundColor", BoundType.Void, [
                ("color", BoundType.Int)
            ]),
   /* 13 */ StaticMethod("SetBackgroundColor", BoundType.Void, [
                ("color", BoundType.Int)
            ]),
   /* 14 */ StaticMethod("SetCursorPosition", BoundType.Void, [
                ("left", BoundType.NullableInt),
                ("top", BoundType.NullableInt)
            ]),
        ]
    );

    internal static ClassSymbol Directory = StaticClass("Directory",
        [
    /* 0 */ StaticMethod("Create", BoundType.Void, [
                ("path", BoundType.String)
            ]),
    /* 1 */ StaticMethod("Delete", BoundType.Void, [
                ("path", BoundType.String)
            ]),
    /* 2 */ StaticMethod("Exists", BoundType.Bool, [
                ("path", BoundType.String)
            ]),
    /* 3 */ StaticMethod("GetCurrentDirectory", BoundType.String, [])
        ]
    );

    internal static ClassSymbol File = StaticClass("File",
        [
    /* 0 */ StaticMethod("AppendText", BoundType.Void, [
                ("fileName", BoundType.String),
                ("text", BoundType.String),
            ]),
    /* 1 */ StaticMethod("Create", BoundType.Void, [
                ("path", BoundType.String)
            ]),
    /* 2 */ StaticMethod("Copy", BoundType.Void, [
                ("sourceFileName", BoundType.String),
                ("destinationFileName", BoundType.String)
            ]),
    /* 3 */ StaticMethod("Delete", BoundType.Void, [
                ("path", BoundType.String)
            ]),
    /* 4 */ StaticMethod("Exists", BoundType.Bool, [
                ("path", BoundType.String)
            ]),
    /* 5 */ StaticMethod("ReadText", BoundType.NullableString, [
                ("fileName", BoundType.String)
            ]),
    /* 6 */ StaticMethod("WriteText", BoundType.Void, [
                ("fileName", BoundType.String),
                ("text", BoundType.String),
            ])
        ]
    );

    internal static ClassSymbol Math = StaticClass("Math",
        [
    /* 0 */ Constexpr("E", BoundType.Decimal, 2.7182818284590451),
    /* 1 */ Constexpr("PI", BoundType.Decimal, 3.1415926535897931),
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

    internal static readonly Dictionary<int, Func<object, object, object, object>> MethodEvaluatorMap
        = new Dictionary<int, Func<object, object, object, object>> {
        { Console.members[1].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => { if (!System.Console.IsOutputRedirected) return System.Console.WindowWidth; return null; }) },
        { Console.members[2].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => { if (!System.Console.IsOutputRedirected) return System.Console.WindowHeight; return null; }) },
        { Console.members[3].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => { if (!System.Console.IsOutputRedirected) return System.Console.ReadLine(); return null; }) },
        { Console.members[4].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => { if (!System.Console.IsOutputRedirected) System.Console.WriteLine(a); return null; }) },
        { Console.members[5].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => { if (!System.Console.IsOutputRedirected) System.Console.WriteLine(a); return null; }) },
        { Console.members[6].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => { if (!System.Console.IsOutputRedirected) System.Console.WriteLine(a); return null; }) },
        { Console.members[7].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => { if (!System.Console.IsOutputRedirected) System.Console.WriteLine(); return null; }) },
        { Console.members[8].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => { if (!System.Console.IsOutputRedirected) System.Console.Write(a); return null; }) },
        { Console.members[9].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => { if (!System.Console.IsOutputRedirected) System.Console.Write(a); return null; }) },
        { Console.members[10].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => { if (!System.Console.IsOutputRedirected) System.Console.Write(a); return null; }) },
        { Console.members[11].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => { if (!System.Console.IsOutputRedirected) System.Console.ResetColor(); return null; }) },
        { Console.members[12].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => {
                if (!System.Console.IsOutputRedirected) System.Console.ForegroundColor = (ConsoleColor)a;
                return null;
               }) },
        { Console.members[13].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => {
                if (!System.Console.IsOutputRedirected) System.Console.BackgroundColor = (ConsoleColor)a;
                return null;
               }) },
        { Console.members[14].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => {
                if (!System.Console.IsOutputRedirected) {
                    System.Console.SetCursorPosition(
                        (int?)a ?? System.Console.CursorLeft,
                        (int?)b ?? System.Console.CursorTop
                    );
                }
                return null;
               }) },
        { Directory.members[0].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => { System.IO.Directory.CreateDirectory((string)a); return null; }) },
        { Directory.members[1].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => { System.IO.Directory.Delete((string)a, true); return null; }) },
        { Directory.members[2].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => { return System.IO.Directory.Exists((string)a); }) },
        { Directory.members[3].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => { return System.IO.Directory.GetCurrentDirectory(); }) },
        { File.members[0].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => { System.IO.File.AppendAllText((string)a, (string)b); return null; }) },
        { File.members[1].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => { System.IO.File.Create((string)a); return null; }) },
        { File.members[2].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => { System.IO.File.Copy((string)a, (string)b); return null; }) },
        { File.members[3].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => { System.IO.File.Delete((string)a); return null; }) },
        { File.members[4].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => { return System.IO.File.Exists((string)a); }) },
        { File.members[5].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => { return System.IO.File.ReadAllText((string)a); }) },
        { File.members[6].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => { System.IO.File.WriteAllText((string)a, (string)b); return null; }) },
        { Math.members[2].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => { return a is null ? null : System.Math.Abs(Convert.ToDouble(a)); }) },
        { Math.members[3].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => { return System.Math.Abs(Convert.ToDouble(a)); }) },
        { Math.members[4].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => { return a is null ? null : System.Math.Abs((int)a); }) },
        { Math.members[5].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => { return System.Math.Abs((int)a); }) },
        { Math.members[6].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => { return a is null ? null : System.Math.Acos(Convert.ToDouble(a)); }) },
        { Math.members[7].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => { return System.Math.Acos(Convert.ToDouble(a)); }) },
        { Math.members[8].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => { return a is null ? null : System.Math.Acosh(Convert.ToDouble(a)); }) },
        { Math.members[9].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => { return System.Math.Acosh(Convert.ToDouble(a)); }) },
        { Math.members[10].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => { return a is null ? null : System.Math.Asin(Convert.ToDouble(a)); }) },
        { Math.members[11].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => { return System.Math.Asin(Convert.ToDouble(a)); }) },
        { Math.members[12].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => { return a is null ? null : System.Math.Asinh(Convert.ToDouble(a)); }) },
        { Math.members[13].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => { return System.Math.Asinh(Convert.ToDouble(a)); }) },
        { Math.members[14].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => { return a is null ? null : System.Math.Atan(Convert.ToDouble(a)); }) },
        { Math.members[15].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => { return System.Math.Atan(Convert.ToDouble(a)); }) },
        { Math.members[16].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => { return a is null ? null : System.Math.Atanh(Convert.ToDouble(a)); }) },
        { Math.members[17].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => { return System.Math.Atanh(Convert.ToDouble(a)); }) },
        { Math.members[18].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => { return a is null ? null : System.Math.Ceiling(Convert.ToDouble(a)); }) },
        { Math.members[19].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => { return System.Math.Ceiling(Convert.ToDouble(a)); }) },
        { Math.members[20].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => {
                return a is null || a is null || a is null ? null :
                    System.Math.Clamp(
                        Convert.ToDouble(a),
                        Convert.ToDouble(b),
                        Convert.ToDouble(c)
                    );
               }) },
        { Math.members[21].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => {
                return System.Math.Clamp(
                    Convert.ToDouble(a),
                    Convert.ToDouble(b),
                    Convert.ToDouble(c)
                );
               }) },
        { Math.members[22].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => {
                return a is null || a is null || a is null ? null :
                    System.Math.Clamp(
                        (int)a,
                        (int)b,
                        (int)c
                    );
               }) },
        { Math.members[23].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => {
                return System.Math.Clamp(
                    (int)a,
                    (int)b,
                    (int)c
                );
               }) },
        { Math.members[24].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => { return a is null ? null : System.Math.Cos(Convert.ToDouble(a)); }) },
        { Math.members[25].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => { return System.Math.Cos(Convert.ToDouble(a)); }) },
        { Math.members[26].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => { return a is null ? null : System.Math.Cosh(Convert.ToDouble(a)); }) },
        { Math.members[27].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => { return System.Math.Cosh(Convert.ToDouble(a)); }) },
        { Math.members[28].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => { return a is null ? null : System.Math.Exp(Convert.ToDouble(a)); }) },
        { Math.members[29].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => { return System.Math.Exp(Convert.ToDouble(a)); }) },
        { Math.members[30].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => { return a is null ? null : System.Math.Floor(Convert.ToDouble(a)); }) },
        { Math.members[31].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => { return System.Math.Floor(Convert.ToDouble(a)); }) },
        { Math.members[32].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => {
                if (a is null || b is null || c is null) return null;
                var rate = Convert.ToDouble(c);
                var start = Convert.ToDouble(a);
                return start + rate * (Convert.ToDouble(b) - start);
               }) },
        { Math.members[33].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => {
                var rate = Convert.ToDouble(c);
                return Convert.ToDouble(a) * (1 - rate) + Convert.ToDouble(b) * rate;
               }) },
        { Math.members[34].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => { return a is null || b is null ? null : System.Math.Log(Convert.ToDouble(a), Convert.ToDouble(b)); }) },
        { Math.members[35].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => { return System.Math.Log(Convert.ToDouble(a), Convert.ToDouble(b)); }) },
        { Math.members[36].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => { return a is null ? null : System.Math.Log(Convert.ToDouble(a)); }) },
        { Math.members[37].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => { return System.Math.Log(Convert.ToDouble(a)); }) },
        { Math.members[38].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => { return a is null || b is null ? null : System.Math.Max(Convert.ToDouble(a), Convert.ToDouble(b)); }) },
        { Math.members[39].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => { return System.Math.Max(Convert.ToDouble(a), Convert.ToDouble(b)); }) },
        { Math.members[40].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => { return a is null || b is null ? null : System.Math.Max((int)a, (int)b); }) },
        { Math.members[41].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => { return System.Math.Max((int)a, (int)b); }) },
        { Math.members[42].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => { return a is null || b is null ? null : System.Math.Min(Convert.ToDouble(a), Convert.ToDouble(b)); }) },
        { Math.members[43].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => { return System.Math.Min(Convert.ToDouble(a), Convert.ToDouble(b)); }) },
        { Math.members[44].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => { return a is null || b is null ? null : System.Math.Min((int)a, (int)b); }) },
        { Math.members[45].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => { return System.Math.Min((int)a, (int)b); }) },
        { Math.members[46].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => { return a is null || b is null ? null : System.Math.Pow(Convert.ToDouble(a), Convert.ToDouble(b)); }) },
        { Math.members[47].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => { return System.Math.Pow(Convert.ToDouble(a), Convert.ToDouble(b)); }) },
        { Math.members[48].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => { return a is null ? null : System.Math.Round(Convert.ToDouble(a)); }) },
        { Math.members[49].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => { return System.Math.Round(Convert.ToDouble(a)); }) },
        { Math.members[50].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => { return a is null ? null : System.Math.Sin(Convert.ToDouble(a)); }) },
        { Math.members[51].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => { return System.Math.Sin(Convert.ToDouble(a)); }) },
        { Math.members[52].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => { return a is null ? null : System.Math.Sinh(Convert.ToDouble(a)); }) },
        { Math.members[53].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => { return System.Math.Sinh(Convert.ToDouble(a)); }) },
        { Math.members[54].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => { return a is null ? null : System.Math.Sqrt(Convert.ToDouble(a)); }) },
        { Math.members[55].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => { return System.Math.Sqrt(Convert.ToDouble(a)); }) },
        { Math.members[56].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => { return a is null ? null : System.Math.Tan(Convert.ToDouble(a)); }) },
        { Math.members[57].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => { return System.Math.Tan(Convert.ToDouble(a)); }) },
        { Math.members[58].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => { return a is null ? null : System.Math.Tanh(Convert.ToDouble(a)); }) },
        { Math.members[59].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => { return System.Math.Tanh(Convert.ToDouble(a)); }) },
        { Math.members[60].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => { return a is null ? null : System.Math.Truncate(Convert.ToDouble(a)); }) },
        { Math.members[61].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => { return System.Math.Truncate(Convert.ToDouble(a)); }) },
    };

    // TODO Ensure this is correct for every other nullable overload
    internal static readonly Dictionary<int, string> MethodTranspilerMap
        = new Dictionary<int, string> {
            { Console.members[1].GetHashCode(), "global::System.Console.WindowWidth" },
            { Console.members[2].GetHashCode(), "global::System.Console.WindowHeight" },
            { Console.members[3].GetHashCode(), "global::System.Console.ReadLine" },
            { Console.members[4].GetHashCode(), "global::System.Console.WriteLine" },
            { Console.members[5].GetHashCode(), "global::System.Console.WriteLine" },
            { Console.members[6].GetHashCode(), "global::System.Console.WriteLine" },
            { Console.members[7].GetHashCode(), "global::System.Console.WriteLine" },
            { Console.members[8].GetHashCode(), "global::System.Console.Write" },
            { Console.members[9].GetHashCode(), "global::System.Console.Write" },
            { Console.members[10].GetHashCode(), "global::System.Console.Write" },
            { Console.members[11].GetHashCode(), "global::System.Console.ResetColor" },
            { Console.members[12].GetHashCode(), "global::System.Console.ForegroundColor = " },
            { Console.members[13].GetHashCode(), "global::System.Console.BackgroundColor = " },
            { Console.members[14].GetHashCode(), "global::System.Console.SetCursorPosition" },
            { Directory.members[0].GetHashCode(), "global::System.IO.Directory.CreateDirectory" },
            { Directory.members[1].GetHashCode(), "global::System.IO.Directory.Delete" },
            { Directory.members[2].GetHashCode(), "global::System.IO.Directory.Exists" },
            { Directory.members[3].GetHashCode(), "global::System.IO.Directory.GetCurrentDirectory" },
            { File.members[0].GetHashCode(), "global::System.IO.File.AppendAllText" },
            { File.members[1].GetHashCode(), "global::System.IO.File.Create" },
            { File.members[2].GetHashCode(), "global::System.IO.File.Copy" },
            { File.members[3].GetHashCode(), "global::System.IO.File.Delete" },
            { File.members[4].GetHashCode(), "global::System.IO.File.Exists" },
            { File.members[5].GetHashCode(), "global::System.IO.File.ReadAllText" },
            { File.members[6].GetHashCode(), "global::System.IO.File.WriteAllText" },
            { Math.members[2].GetHashCode(), "global::System.Math.Abs" },
            { Math.members[3].GetHashCode(), "global::System.Math.Abs" },
            { Math.members[4].GetHashCode(), "global::System.Math.Abs" },
            { Math.members[5].GetHashCode(), "global::System.Math.Abs" },
            { Math.members[6].GetHashCode(), "global::System.Math.Acos" },
            { Math.members[7].GetHashCode(), "global::System.Math.Acos" },
            { Math.members[8].GetHashCode(), "global::System.Math.Acosh" },
            { Math.members[9].GetHashCode(), "global::System.Math.Acosh" },
            { Math.members[10].GetHashCode(), "global::System.Math.Asin" },
            { Math.members[11].GetHashCode(), "global::System.Math.Asin" },
            { Math.members[12].GetHashCode(), "global::System.Math.Asinh" },
            { Math.members[13].GetHashCode(), "global::System.Math.Asinh" },
            { Math.members[14].GetHashCode(), "global::System.Math.Atan" },
            { Math.members[15].GetHashCode(), "global::System.Math.Atan" },
            { Math.members[16].GetHashCode(), "global::System.Math.Atanh" },
            { Math.members[17].GetHashCode(), "global::System.Math.Atanh" },
            { Math.members[18].GetHashCode(), "global::System.Math.Ceiling" },
            { Math.members[19].GetHashCode(), "global::System.Math.Ceiling" },
            { Math.members[20].GetHashCode(), "global::System.Math.Clamp" },
            { Math.members[21].GetHashCode(), "global::System.Math.Clamp" },
            { Math.members[22].GetHashCode(), "global::System.Math.Clamp" },
            { Math.members[23].GetHashCode(), "global::System.Math.Clamp" },
            { Math.members[24].GetHashCode(), "global::System.Math.Cos" },
            { Math.members[25].GetHashCode(), "global::System.Math.Cos" },
            { Math.members[26].GetHashCode(), "global::System.Math.Cosh" },
            { Math.members[27].GetHashCode(), "global::System.Math.Cosh" },
            { Math.members[28].GetHashCode(), "global::System.Math.Exp" },
            { Math.members[29].GetHashCode(), "global::System.Math.Exp" },
            { Math.members[30].GetHashCode(), "global::System.Math.Floor" },
            { Math.members[31].GetHashCode(), "global::System.Math.Floor" },
            { Math.members[32].GetHashCode(),
                "((Func<double, double, double, double>)((x, y, z) => { return x * (1 - z) + y * z; } ))" },
            { Math.members[33].GetHashCode(),
                "((Func<double, double, double, double>)((x, y, z) => { return x * (1 - z) + y * z; } ))" },
            { Math.members[34].GetHashCode(), "global::System.Math.Log" },
            { Math.members[35].GetHashCode(), "global::System.Math.Log" },
            { Math.members[36].GetHashCode(), "global::System.Math.Log" },
            { Math.members[37].GetHashCode(), "global::System.Math.Log" },
            { Math.members[38].GetHashCode(), "global::System.Math.Max" },
            { Math.members[39].GetHashCode(), "global::System.Math.Max" },
            { Math.members[40].GetHashCode(), "global::System.Math.Max" },
            { Math.members[41].GetHashCode(), "global::System.Math.Max" },
            { Math.members[42].GetHashCode(), "global::System.Math.Min" },
            { Math.members[43].GetHashCode(), "global::System.Math.Min" },
            { Math.members[44].GetHashCode(), "global::System.Math.Min" },
            { Math.members[45].GetHashCode(), "global::System.Math.Min" },
            { Math.members[46].GetHashCode(), "global::System.Math.Pow" },
            { Math.members[47].GetHashCode(), "global::System.Math.Pow" },
            { Math.members[48].GetHashCode(), "global::System.Math.Round" },
            { Math.members[49].GetHashCode(), "global::System.Math.Round" },
            { Math.members[50].GetHashCode(), "global::System.Math.Sin" },
            { Math.members[51].GetHashCode(), "global::System.Math.Sin" },
            { Math.members[52].GetHashCode(), "global::System.Math.Sinh" },
            { Math.members[53].GetHashCode(), "global::System.Math.Sinh" },
            { Math.members[54].GetHashCode(), "global::System.Math.Sqrt" },
            { Math.members[55].GetHashCode(), "global::System.Math.Sqrt" },
            { Math.members[56].GetHashCode(), "global::System.Math.Tan" },
            { Math.members[57].GetHashCode(), "global::System.Math.Tan" },
            { Math.members[58].GetHashCode(), "global::System.Math.Tanh" },
            { Math.members[59].GetHashCode(), "global::System.Math.Tanh" },
            { Math.members[60].GetHashCode(), "global::System.Math.Truncate" },
            { Math.members[61].GetHashCode(), "global::System.Math.Truncate" },
        };

    /// <summary>
    /// Initializes all of the Standard Library types.
    /// </summary>
    internal static void Load() {
        // This is how many members we statically declared on Object
        // This ensures that if Load() is called multiple times, the new members don't get added multiple times
        if (Object.members.Length > 2)
            return;

        Object.UpdateInternals(
            Object.templateParameters,
            Object.templateConstraints,
            Object.members.AddRange(
        /* 2 */ Method(
                    "Equals",
                    BoundType.Bool,
                    [("object", new BoundType(Object, isNullable: true))],
                    DeclarationModifiers.Virtual,
                    Accessibility.Public,
                    MethodDeclaration(
                        null,
                        TokenList(Token(SyntaxKind.VirtualKeyword)),
                        NonNullableType("bool"),
                        Identifier("Equals"),
                        TemplateParameterList(),
                        ParameterList(
                            Token(SyntaxKind.OpenParenToken),
                            SeparatedList(
                                Parameter(IdentifierName("Object"), Identifier("object"))
                            ),
                            Token(SyntaxKind.CloseParenToken)
                        ),
                        ConstraintClauseList(),
                        Block(
                            If(
                                BinaryExpression(
                                    IdentifierName("object"),
                                    Token(SyntaxKind.IsKeyword),
                                    LiteralExpression(Token(SyntaxKind.NullKeyword))
                                ),
                                Return(Literal(false))
                            ),
                            Return(
                                PostfixExpression(
                                    CallExpression(
                                        IdentifierName("ObjectsEqual"),
                                        ArgumentList(
                                            Argument(This()),
                                            Argument(IdentifierName("object"))
                                        )
                                    ),
                                    Token(SyntaxKind.ExclamationToken)
                                )
                            )
                        ),
                        Token(SyntaxKind.SemicolonToken)
                    )
                ),
        /* 3 */ Method(
                    "ReferenceEquals",
                    BoundType.Bool,
                    [("object", new BoundType(Object, isReference: true, isNullable: true))],
                    DeclarationModifiers.Virtual,
                    Accessibility.Public,
                    MethodDeclaration(
                        null,
                        TokenList(Token(SyntaxKind.VirtualKeyword)),
                        NonNullableType("bool"),
                        Identifier("ReferenceEquals"),
                        TemplateParameterList(),
                        ParameterList(
                            Token(SyntaxKind.OpenParenToken),
                            SeparatedList(
                                Parameter(ReferenceType("Object"), Identifier("object"))
                            ),
                            Token(SyntaxKind.CloseParenToken)
                        ),
                        ConstraintClauseList(),
                        Block(
                            If(
                                BinaryExpression(
                                    IdentifierName("object"),
                                    Token(SyntaxKind.IsKeyword),
                                    LiteralExpression(Token(SyntaxKind.NullKeyword))
                                ),
                                Return(Literal(false))
                            ),
                            Return(
                                PostfixExpression(
                                    CallExpression(
                                        IdentifierName("ObjectReferencesEqual"),
                                        ArgumentList(
                                            Argument(This()),
                                            Argument(IdentifierName("object"))
                                        )
                                    ),
                                    Token(SyntaxKind.ExclamationToken)
                                )
                            )
                        ),
                        Token(SyntaxKind.SemicolonToken)
                    )
                ),
        /* 4 */ Method(
                    "op_Equality",
                    BoundType.NullableBool,
                    [("x", new BoundType(Object, isNullable: true)), ("y", new BoundType(Object, isNullable: true))],
                    DeclarationModifiers.Static,
                    Accessibility.Public,
                    MethodDeclaration(
                        null,
                        TokenList(Token(SyntaxKind.StaticKeyword)),
                        IdentifierName("bool"),
                        Identifier("op_Equality"),
                        TemplateParameterList(),
                        ParameterList(
                            Token(SyntaxKind.OpenParenToken),
                            SeparatedList(
                                Parameter(ReferenceType("Object"), Identifier("x")),
                                Parameter(ReferenceType("Object"), Identifier("y"))
                            ),
                            Token(SyntaxKind.CloseParenToken)
                        ),
                        ConstraintClauseList(),
                        Block(
                            Return(
                                CallExpression(
                                    IdentifierName("ObjectsEqual"),
                                    ArgumentList(
                                        Argument(IdentifierName("x")),
                                        Argument(IdentifierName("y"))
                                    )
                                )
                            )
                        ),
                        Token(SyntaxKind.SemicolonToken)
                    )
                ),
        /* 5 */ Method(
                    "op_Inequality",
                    BoundType.NullableBool,
                    [("x", new BoundType(Object, isNullable: true)), ("y", new BoundType(Object, isNullable: true))],
                    DeclarationModifiers.Static,
                    Accessibility.Public,
                    MethodDeclaration(
                        null,
                        TokenList(Token(SyntaxKind.StaticKeyword)),
                        IdentifierName("bool"),
                        Identifier("op_Inequality"),
                        TemplateParameterList(),
                        ParameterList(
                            Token(SyntaxKind.OpenParenToken),
                            SeparatedList(
                                Parameter(ReferenceType("Object"), Identifier("x")),
                                Parameter(ReferenceType("Object"), Identifier("y"))
                            ),
                            Token(SyntaxKind.CloseParenToken)
                        ),
                        ConstraintClauseList(),
                        Block(
                            Return(
                                UnaryExpression(
                                    Token(SyntaxKind.ExclamationToken),
                                    CallExpression(
                                        IdentifierName("ObjectsEqual"),
                                        ArgumentList(
                                            Argument(IdentifierName("x")),
                                            Argument(IdentifierName("y"))
                                        )
                                    )
                                )
                            )
                        ),
                        Token(SyntaxKind.SemicolonToken)
                    )
                ),
        /* 6 */ Method(
                    "GetHashCode",
                    BoundType.Int,
                    [],
                    DeclarationModifiers.Virtual,
                    Accessibility.Public,
                    MethodDeclaration(
                        null,
                        TokenList(Token(SyntaxKind.VirtualKeyword)),
                        IdentifierName("int"),
                        Identifier("GetHashCode"),
                        TemplateParameterList(),
                        ParameterList(
                            Token(SyntaxKind.OpenParenToken),
                            SeparatedList(
                                Parameter(IdentifierName("any"), Identifier("value"))
                            ),
                            Token(SyntaxKind.CloseParenToken)
                        ),
                        ConstraintClauseList(),
                        Block(
                            Return(
                                CallExpression(
                                    IdentifierName("GetHashCode"),
                                    ArgumentList(Argument(This()))
                                )
                            )
                        ),
                        Token(SyntaxKind.SemicolonToken)
                    )
                )
            )
        );
    }

    /// <summary>
    /// Updates all of the Standard Library types that require WellKnownTypes.
    /// </summary>
    internal static void UpdateLibraries(Dictionary<string, NamedTypeSymbol> wellKnownTypes) {
        // If the List type is not found, that means none of these methods are being called so it does not matter
        // what type we put in List's place. Using Void here arbitrarily. We still need to declare these methods though,
        // because the Evaluator will check for them.
        wellKnownTypes.TryGetValue(WellKnownTypeNames.List, out var listTypeSymbol);
        var listStringType = listTypeSymbol is null
            ? BoundType.Void
            : new BoundType(listTypeSymbol, templateArguments: [new TypeOrConstant(BoundType.String)]);

        if (Directory.members.Length > 4)
            return;

        Directory.UpdateInternals(
            Directory.templateParameters,
            Directory.templateConstraints,
            Directory.members.AddRange(
        /* 4 */ StaticMethod("GetDirectories", listStringType, [
                    ("path", BoundType.String)
                ]),
        /* 5 */ StaticMethod("GetFiles", listStringType, [
                    ("path", BoundType.String)
                ])
            )
        );

        if (File.members.Length > 7)
            return;

        File.UpdateInternals(
            File.templateParameters,
            File.templateConstraints,
            File.members.AddRange(
        /* 7 */ StaticMethod("AppendLines", BoundType.Void, [
                    ("fileName", BoundType.String),
                    ("lines", listStringType),
                ]),
        /* 8 */ StaticMethod("ReadLines", listStringType, [
                    ("fileName", BoundType.String)
                ]),
        /* 9 */ StaticMethod("WriteLines", BoundType.Void, [
                    ("fileName", BoundType.String),
                    ("lines", listStringType),
                ])
            )
        );

        var length = MethodEvaluatorMap.Count;

        MethodEvaluatorMap.Add(Directory.members[4].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => { return System.IO.Directory.GetDirectories((string)a); }));
        if (MethodEvaluatorMap.Count > length + 1) return;
        MethodEvaluatorMap.Add(Directory.members[5].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => { return System.IO.Directory.GetFiles((string)a); }));
        if (MethodEvaluatorMap.Count > length + 2) return;
        MethodEvaluatorMap.Add(File.members[7].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => { System.IO.File.AppendAllLines((string)a, (List<string>)b); return null; }));
        if (MethodEvaluatorMap.Count > length + 3) return;
        MethodEvaluatorMap.Add(File.members[8].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => { return System.IO.File.ReadAllLines((string)a); }));
        if (MethodEvaluatorMap.Count > length + 4) return;
        MethodEvaluatorMap.Add(File.members[9].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            => { System.IO.File.WriteAllLines((string)a, (List<string>)b); return null; }));

        MethodTranspilerMap.Add(Directory.members[4].GetHashCode(), "global::System.IO.Directory.GetDirectories");
        MethodTranspilerMap.Add(Directory.members[5].GetHashCode(), "global::System.IO.Directory.GetFiles");
        MethodTranspilerMap.Add(File.members[7].GetHashCode(), "global::System.IO.File.AppendAllLines");
        MethodTranspilerMap.Add(File.members[8].GetHashCode(), "global::System.IO.File.ReadAllLines");
        MethodTranspilerMap.Add(File.members[9].GetHashCode(), "global::System.IO.File.WriteAllLines");
    }

    /// <summary>
    /// Gets all the pre-compiled symbols defined by the library.
    /// </summary>
    internal static Symbol[] GetSymbols() {
        return [Object, Console, Directory, File, Math];
    }

    /// <summary>
    /// Converts a Standard Library method name into its C# equivalent as a string.
    /// </summary>
    internal static string CSharpEmitMethod(MethodSymbol method) {
        return MethodTranspilerMap[method.GetHashCode()];
    }
}
