using System;
using System.Collections.Generic;
using System.Threading;
using Buckle.CodeAnalysis.Symbols;
using static Buckle.Libraries.LibraryHelpers;

namespace Buckle.Libraries;

internal static partial class StandardLibrary {
    private static SynthesizedFinishedNamedTypeSymbol _lazyDirectory;
    private static SynthesizedFinishedNamedTypeSymbol _lazyFile;
    private static SynthesizedFinishedNamedTypeSymbol _lazyConsole;
    private static SynthesizedFinishedNamedTypeSymbol _lazyMath;
    private static Dictionary<string, Func<object, object, object, object>> _lazyEvaluatorMap;

    internal static SynthesizedFinishedNamedTypeSymbol Directory {
        get {
            if (_lazyDirectory is null)
                Interlocked.CompareExchange(ref _lazyDirectory, GenerateDirectory(), null);

            return _lazyDirectory;
        }
    }

    internal static SynthesizedFinishedNamedTypeSymbol File {
        get {
            if (_lazyFile is null)
                Interlocked.CompareExchange(ref _lazyFile, GenerateFile(), null);

            return _lazyFile;
        }
    }

    internal static SynthesizedFinishedNamedTypeSymbol Console {
        get {
            if (_lazyConsole is null)
                Interlocked.CompareExchange(ref _lazyConsole, GenerateConsole(), null);

            return _lazyConsole;
        }
    }

    internal static SynthesizedFinishedNamedTypeSymbol Math {
        get {
            if (_lazyMath is null)
                Interlocked.CompareExchange(ref _lazyMath, GenerateMath(), null);

            return _lazyMath;
        }
    }

    internal static Dictionary<string, Func<object, object, object, object>> EvaluatorMap {
        get {
            if (_lazyEvaluatorMap is null)
                Interlocked.CompareExchange(ref _lazyEvaluatorMap, GenerateEvaluatorMap(), null);

            return _lazyEvaluatorMap;
        }
    }

    internal static IEnumerable<SynthesizedFinishedNamedTypeSymbol> GetTypes() {
        // yield return Directory;
        // yield return File;
        yield return Console;
        yield return Math;
    }

    private static SynthesizedFinishedNamedTypeSymbol GenerateDirectory() {
        return StaticClass("Directory", [
            StaticMethod("Create", SpecialType.Void, [("path", SpecialType.String)]),
            StaticMethod("Delete", SpecialType.Void, [("path", SpecialType.String)]),
            StaticMethod("Exists", SpecialType.Bool, [("path", SpecialType.String)]),
            StaticMethod("GetCurrentDirectory", SpecialType.String),
            StaticMethod("GetDirectories", StringList, [("path", SpecialType.String)]),
            StaticMethod("GetFiles", StringList, [("path", SpecialType.String)]),
        ]);
    }

    private static SynthesizedFinishedNamedTypeSymbol GenerateFile() {
        return StaticClass("File", [
            StaticMethod("AppendLines", SpecialType.Void, [("fileName", SpecialType.String), ("lines", StringList)]),
            StaticMethod("AppendText", SpecialType.Void, [("fileName", SpecialType.String), ("text", SpecialType.String)]),
            StaticMethod("Create", SpecialType.Void, [("path", SpecialType.String)]),
            StaticMethod("Copy", SpecialType.Void, [("sourceFileName", SpecialType.String), ("destinationFileName", SpecialType.String)]),
            StaticMethod("Delete", SpecialType.Void, [("path", SpecialType.String)]),
            StaticMethod("Exists", SpecialType.Bool, [("path", SpecialType.String)]),
            StaticMethod("ReadLines", StringList, [("fileName", SpecialType.String)]),
            StaticMethod("ReadText", SpecialType.String, true, [("fileName", SpecialType.String)]),
            StaticMethod("WriteLines", SpecialType.Void, [("fileName", SpecialType.String), ("lines", StringList)]),
            StaticMethod("WriteText", SpecialType.Void, [("fileName", SpecialType.String), ("text", SpecialType.String)]),
        ]);
    }

    private static SynthesizedFinishedNamedTypeSymbol GenerateConsole() {
        return StaticClass("Console", [
            StaticClass("Color", [
                ConstExprField("Black", SpecialType.Int, 0),
                ConstExprField("DarkBlue", SpecialType.Int, 1),
                ConstExprField("DarkGreen", SpecialType.Int, 2),
                ConstExprField("DarkCyan", SpecialType.Int, 3),
                ConstExprField("DarkRed", SpecialType.Int, 4),
                ConstExprField("DarkMagenta", SpecialType.Int, 5),
                ConstExprField("DarkYellow", SpecialType.Int, 6),
                ConstExprField("Gray", SpecialType.Int, 7),
                ConstExprField("DarkGray", SpecialType.Int, 8),
                ConstExprField("Blue", SpecialType.Int, 9),
                ConstExprField("Green", SpecialType.Int, 10),
                ConstExprField("Cyan", SpecialType.Int, 11),
                ConstExprField("Red", SpecialType.Int, 12),
                ConstExprField("Magenta", SpecialType.Int, 13),
                ConstExprField("Yellow", SpecialType.Int, 14),
                ConstExprField("White", SpecialType.Int, 15)
            ]),
            StaticMethod("GetWidth", SpecialType.Int),
            StaticMethod("GetHeight", SpecialType.Int),
            StaticMethod("Input", SpecialType.String),
            StaticMethod("PrintLine", SpecialType.Void),
            StaticMethod("PrintLine", SpecialType.Void, [("message", SpecialType.String, true)]),
            StaticMethod("PrintLine", SpecialType.Void, [("value", SpecialType.Any, true)]),
            StaticMethod("PrintLine", SpecialType.Void, [("object", SpecialType.Object, true)]),
            StaticMethod("Print", SpecialType.Void, [("message", SpecialType.String, true)]),
            StaticMethod("Print", SpecialType.Void, [("value", SpecialType.Any, true)]),
            StaticMethod("Print", SpecialType.Void, [("object", SpecialType.Object, true)]),
            StaticMethod("ResetColor", SpecialType.Void),
            StaticMethod("SetForegroundColor", SpecialType.Void, [("color", SpecialType.Int)]),
            StaticMethod("SetBackgroundColor", SpecialType.Void, [("color", SpecialType.Int)]),
            StaticMethod("SetCursorPosition", SpecialType.Void, [("left", SpecialType.Int, true), ("top", SpecialType.Int, true)]),
        ]);
    }

    private static SynthesizedFinishedNamedTypeSymbol GenerateMath() {
        return StaticClass("Math", [
            ConstExprField("E", SpecialType.Decimal, 2.7182818284590451),
            ConstExprField("PI", SpecialType.Decimal, 3.1415926535897931),
            StaticMethod("Abs", SpecialType.Decimal, true, [("value", SpecialType.Decimal, true)]),
            StaticMethod("Abs", SpecialType.Decimal, [("value", SpecialType.Decimal)]),
            StaticMethod("Abs", SpecialType.Int, true, [("value", SpecialType.Int, true)]),
            StaticMethod("Abs", SpecialType.Int, [("value", SpecialType.Int)]),
            StaticMethod("Acos", SpecialType.Decimal, true, [("d", SpecialType.Decimal, true)]),
            StaticMethod("Acos", SpecialType.Decimal, [("d", SpecialType.Decimal)]),
            StaticMethod("Acosh", SpecialType.Decimal, true, [("d", SpecialType.Decimal, true)]),
            StaticMethod("Acosh", SpecialType.Decimal, [("d", SpecialType.Decimal)]),
            StaticMethod("Asin", SpecialType.Decimal, true, [("d", SpecialType.Decimal, true)]),
            StaticMethod("Asin", SpecialType.Decimal, [("d", SpecialType.Decimal)]),
            StaticMethod("Asinh", SpecialType.Decimal, true, [("d", SpecialType.Decimal, true)]),
            StaticMethod("Asinh", SpecialType.Decimal, [("d", SpecialType.Decimal)]),
            StaticMethod("Atan", SpecialType.Decimal, true, [("d", SpecialType.Decimal, true)]),
            StaticMethod("Atan", SpecialType.Decimal, [("d", SpecialType.Decimal)]),
            StaticMethod("Atanh", SpecialType.Decimal, true, [("d", SpecialType.Decimal, true)]),
            StaticMethod("Atanh", SpecialType.Decimal, [("d", SpecialType.Decimal)]),
            StaticMethod("Ceiling", SpecialType.Decimal, true, [("d", SpecialType.Decimal, true)]),
            StaticMethod("Ceiling", SpecialType.Decimal, [("d", SpecialType.Decimal)]),
            StaticMethod("Clamp", SpecialType.Decimal, true, [("value", SpecialType.Decimal, true), ("min", SpecialType.Decimal, true), ("max", SpecialType.Decimal, true)]),
            StaticMethod("Clamp", SpecialType.Decimal, [("value", SpecialType.Decimal), ("min", SpecialType.Decimal), ("max", SpecialType.Decimal)]),
            StaticMethod("Clamp", SpecialType.Int, true, [("value", SpecialType.Int, true), ("min", SpecialType.Int, true), ("max", SpecialType.Int, true)]),
            StaticMethod("Clamp", SpecialType.Int, [("value", SpecialType.Int), ("min", SpecialType.Int), ("max", SpecialType.Int)]),
            StaticMethod("Cos", SpecialType.Decimal, true, [("d", SpecialType.Decimal, true)]),
            StaticMethod("Cos", SpecialType.Decimal, [("d", SpecialType.Decimal)]),
            StaticMethod("Cosh", SpecialType.Decimal, true, [("d", SpecialType.Decimal, true)]),
            StaticMethod("Cosh", SpecialType.Decimal, [("d", SpecialType.Decimal)]),
            StaticMethod("Exp", SpecialType.Decimal, true, [("d", SpecialType.Decimal, true)]),
            StaticMethod("Exp", SpecialType.Decimal, [("d", SpecialType.Decimal)]),
            StaticMethod("Floor", SpecialType.Decimal, true, [("d", SpecialType.Decimal, true)]),
            StaticMethod("Floor", SpecialType.Decimal, [("d", SpecialType.Decimal)]),
            StaticMethod("Lerp", SpecialType.Decimal, true, [("start", SpecialType.Decimal, true), ("end", SpecialType.Decimal, true), ("rate", SpecialType.Decimal, true)]),
            StaticMethod("Lerp", SpecialType.Decimal, [("start", SpecialType.Decimal), ("end", SpecialType.Decimal), ("rate", SpecialType.Decimal)]),
            StaticMethod("Log", SpecialType.Decimal, true, [("d", SpecialType.Decimal, true), ("base", SpecialType.Decimal, true)]),
            StaticMethod("Log", SpecialType.Decimal, [("d", SpecialType.Decimal), ("base", SpecialType.Decimal)]),
            StaticMethod("Log", SpecialType.Decimal, true, [("d", SpecialType.Decimal, true)]),
            StaticMethod("Log", SpecialType.Decimal, [("d", SpecialType.Decimal)]),
            StaticMethod("Max", SpecialType.Decimal, true, [("val1", SpecialType.Decimal, true), ("val2", SpecialType.Decimal, true)]),
            StaticMethod("Max", SpecialType.Decimal, [("val1", SpecialType.Decimal), ("val2", SpecialType.Decimal)]),
            StaticMethod("Max", SpecialType.Int, true, [("val1", SpecialType.Int, true), ("val2", SpecialType.Int, true)]),
            StaticMethod("Max", SpecialType.Int, [("val1", SpecialType.Int), ("val2", SpecialType.Int)]),
            StaticMethod("Min", SpecialType.Decimal, true, [("val1", SpecialType.Decimal, true), ("val2", SpecialType.Decimal, true)]),
            StaticMethod("Min", SpecialType.Decimal, [("val1", SpecialType.Decimal), ("val2", SpecialType.Decimal)]),
            StaticMethod("Min", SpecialType.Int, true, [("val1", SpecialType.Int, true), ("val2", SpecialType.Int, true)]),
            StaticMethod("Min", SpecialType.Int, [("val1", SpecialType.Int), ("val2", SpecialType.Int)]),
            StaticMethod("Pow", SpecialType.Decimal, true, [("x", SpecialType.Decimal, true), ("y", SpecialType.Decimal, true)]),
            StaticMethod("Pow", SpecialType.Decimal, [("x", SpecialType.Decimal), ("y", SpecialType.Decimal)]),
            StaticMethod("Round", SpecialType.Decimal, true, [("value", SpecialType.Decimal, true)]),
            StaticMethod("Round", SpecialType.Decimal, [("value", SpecialType.Decimal)]),
            StaticMethod("Sin", SpecialType.Decimal, true, [("d", SpecialType.Decimal, true)]),
            StaticMethod("Sin", SpecialType.Decimal, [("d", SpecialType.Decimal)]),
            StaticMethod("Sinh", SpecialType.Decimal, true, [("d", SpecialType.Decimal, true)]),
            StaticMethod("Sinh", SpecialType.Decimal, [("d", SpecialType.Decimal)]),
            StaticMethod("Sqrt", SpecialType.Decimal, true, [("d", SpecialType.Decimal, true)]),
            StaticMethod("Sqrt", SpecialType.Decimal, [("d", SpecialType.Decimal)]),
            StaticMethod("Tan", SpecialType.Decimal, true, [("d", SpecialType.Decimal, true)]),
            StaticMethod("Tan", SpecialType.Decimal, [("d", SpecialType.Decimal)]),
            StaticMethod("Tanh", SpecialType.Decimal, true, [("d", SpecialType.Decimal, true)]),
            StaticMethod("Tanh", SpecialType.Decimal, [("d", SpecialType.Decimal)]),
            StaticMethod("Truncate", SpecialType.Decimal, true, [("value", SpecialType.Decimal, true)]),
            StaticMethod("Truncate", SpecialType.Decimal, [("value", SpecialType.Decimal)]),
        ]);
    }

    private static Dictionary<string, Func<object, object, object, object>> GenerateEvaluatorMap() {
        return new Dictionary<string, Func<object, object, object, object>>() {
            { "Console_GetWidth", new Func<object, object, object, object>((a, b, c)
                => { if (!System.Console.IsOutputRedirected) return System.Console.WindowWidth; return null; }) },
            { "Console_GetHeight", new Func<object, object, object, object>((a, b, c)
                => { if (!System.Console.IsOutputRedirected) return System.Console.WindowHeight; return null; }) },
            { "Console_Input", new Func<object, object, object, object>((a, b, c)
                => { if (!System.Console.IsOutputRedirected) return System.Console.ReadLine(); return null; }) },
            { "Console_PrintLine", new Func<object, object, object, object>((a, b, c)
                => { if (!System.Console.IsOutputRedirected) System.Console.WriteLine(a); return null; }) },
            { "Console_PrintLine_S", new Func<object, object, object, object>((a, b, c)
                => { if (!System.Console.IsOutputRedirected) System.Console.WriteLine(a); return null; }) },
            { "Console_PrintLine_A", new Func<object, object, object, object>((a, b, c)
                => { if (!System.Console.IsOutputRedirected) System.Console.WriteLine(a); return null; }) },
            { "Console_PrintLine_O", new Func<object, object, object, object>((a, b, c)
                => { if (!System.Console.IsOutputRedirected) System.Console.WriteLine(); return null; }) },
            { "Console_Print_S", new Func<object, object, object, object>((a, b, c)
                => { if (!System.Console.IsOutputRedirected) System.Console.Write(a); return null; }) },
            { "Console_Print_A", new Func<object, object, object, object>((a, b, c)
                => { if (!System.Console.IsOutputRedirected) System.Console.Write(a); return null; }) },
            { "Console_Print_O", new Func<object, object, object, object>((a, b, c)
                => { if (!System.Console.IsOutputRedirected) System.Console.Write(a); return null; }) },
            { "Console_ResetColor", new Func<object, object, object, object>((a, b, c)
                => { if (!System.Console.IsOutputRedirected) System.Console.ResetColor(); return null; }) },
            { "Console_SetForegroundColor_I", new Func<object, object, object, object>((a, b, c)
                => { if (!System.Console.IsOutputRedirected) System.Console.ForegroundColor = (ConsoleColor)a; return null; }) },
            { "Console_SetBackgroundColor_I", new Func<object, object, object, object>((a, b, c)
                => { if (!System.Console.IsOutputRedirected) System.Console.BackgroundColor = (ConsoleColor)a; return null; }) },
            { "Console_SetCursorPosition_II", new Func<object, object, object, object>((a, b, c)
                => { if (!System.Console.IsOutputRedirected) { System.Console.SetCursorPosition((int?)a ?? System.Console.CursorLeft, (int?)b ?? System.Console.CursorTop); } return null; }) },
            // { Directory.members[0].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            //     => { System.IO.Directory.CreateDirectory((string)a); return null; }) },
            // { Directory.members[1].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            //     => { System.IO.Directory.Delete((string)a, true); return null; }) },
            // { Directory.members[2].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            //     => { return System.IO.Directory.Exists((string)a); }) },
            // { Directory.members[3].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            //     => { return System.IO.Directory.GetCurrentDirectory(); }) },
            // { File.members[0].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            //     => { System.IO.File.AppendAllText((string)a, (string)b); return null; }) },
            // { File.members[1].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            //     => { System.IO.File.Create((string)a); return null; }) },
            // { File.members[2].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            //     => { System.IO.File.Copy((string)a, (string)b); return null; }) },
            // { File.members[3].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            //     => { System.IO.File.Delete((string)a); return null; }) },
            // { File.members[4].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            //     => { return System.IO.File.Exists((string)a); }) },
            // { File.members[5].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            //     => { return System.IO.File.ReadAllText((string)a); }) },
            // { File.members[6].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            //     => { System.IO.File.WriteAllText((string)a, (string)b); return null; }) },
            // { Math.members[2].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            //     => { return a is null ? null : System.Math.Abs(Convert.ToDouble(a)); }) },
            // { Math.members[3].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            //     => { return System.Math.Abs(Convert.ToDouble(a)); }) },
            // { Math.members[4].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            //     => { return a is null ? null : System.Math.Abs((int)a); }) },
            // { Math.members[5].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            //     => { return System.Math.Abs((int)a); }) },
            // { Math.members[6].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            //     => { return a is null ? null : System.Math.Acos(Convert.ToDouble(a)); }) },
            // { Math.members[7].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            //     => { return System.Math.Acos(Convert.ToDouble(a)); }) },
            // { Math.members[8].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            //     => { return a is null ? null : System.Math.Acosh(Convert.ToDouble(a)); }) },
            // { Math.members[9].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            //     => { return System.Math.Acosh(Convert.ToDouble(a)); }) },
            // { Math.members[10].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            //     => { return a is null ? null : System.Math.Asin(Convert.ToDouble(a)); }) },
            // { Math.members[11].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            //     => { return System.Math.Asin(Convert.ToDouble(a)); }) },
            // { Math.members[12].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            //     => { return a is null ? null : System.Math.Asinh(Convert.ToDouble(a)); }) },
            // { Math.members[13].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            //     => { return System.Math.Asinh(Convert.ToDouble(a)); }) },
            // { Math.members[14].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            //     => { return a is null ? null : System.Math.Atan(Convert.ToDouble(a)); }) },
            // { Math.members[15].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            //     => { return System.Math.Atan(Convert.ToDouble(a)); }) },
            // { Math.members[16].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            //     => { return a is null ? null : System.Math.Atanh(Convert.ToDouble(a)); }) },
            // { Math.members[17].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            //     => { return System.Math.Atanh(Convert.ToDouble(a)); }) },
            // { Math.members[18].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            //     => { return a is null ? null : System.Math.Ceiling(Convert.ToDouble(a)); }) },
            // { Math.members[19].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            //     => { return System.Math.Ceiling(Convert.ToDouble(a)); }) },
            // { Math.members[20].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            //     => { return a is null || a is null || a is null ? null : System.Math.Clamp(Convert.ToDouble(a), Convert.ToDouble(b), Convert.ToDouble(c)); }) },
            // { Math.members[21].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            //     => { return System.Math.Clamp(Convert.ToDouble(a), Convert.ToDouble(b), Convert.ToDouble(c)); }) },
            // { Math.members[22].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            //     => { return a is null || a is null || a is null ? null : System.Math.Clamp((int)a, (int)b, (int)c); }) },
            // { Math.members[23].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            //     => { return System.Math.Clamp((int)a, (int)b, (int)c); }) },
            // { Math.members[24].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            //     => { return a is null ? null : System.Math.Cos(Convert.ToDouble(a)); }) },
            // { Math.members[25].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            //     => { return System.Math.Cos(Convert.ToDouble(a)); }) },
            // { Math.members[26].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            //     => { return a is null ? null : System.Math.Cosh(Convert.ToDouble(a)); }) },
            // { Math.members[27].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            //     => { return System.Math.Cosh(Convert.ToDouble(a)); }) },
            // { Math.members[28].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            //     => { return a is null ? null : System.Math.Exp(Convert.ToDouble(a)); }) },
            // { Math.members[29].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            //     => { return System.Math.Exp(Convert.ToDouble(a)); }) },
            // { Math.members[30].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            //     => { return a is null ? null : System.Math.Floor(Convert.ToDouble(a)); }) },
            // { Math.members[31].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            //     => { return System.Math.Floor(Convert.ToDouble(a)); }) },
            // { Math.members[32].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            //     => { if (a is null || b is null || c is null) return null; var rate = Convert.ToDouble(c); var start = Convert.ToDouble(a); return start + rate * (Convert.ToDouble(b) - start); }) },
            // { Math.members[33].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            //     => { var rate = Convert.ToDouble(c); return Convert.ToDouble(a) * (1 - rate) + Convert.ToDouble(b) * rate; }) },
            // { Math.members[34].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            //     => { return a is null || b is null ? null : System.Math.Log(Convert.ToDouble(a), Convert.ToDouble(b)); }) },
            // { Math.members[35].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            //     => { return System.Math.Log(Convert.ToDouble(a), Convert.ToDouble(b)); }) },
            // { Math.members[36].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            //     => { return a is null ? null : System.Math.Log(Convert.ToDouble(a)); }) },
            // { Math.members[37].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            //     => { return System.Math.Log(Convert.ToDouble(a)); }) },
            // { Math.members[38].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            //     => { return a is null || b is null ? null : System.Math.Max(Convert.ToDouble(a), Convert.ToDouble(b)); }) },
            // { Math.members[39].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            //     => { return System.Math.Max(Convert.ToDouble(a), Convert.ToDouble(b)); }) },
            // { Math.members[40].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            //     => { return a is null || b is null ? null : System.Math.Max((int)a, (int)b); }) },
            // { Math.members[41].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            //     => { return System.Math.Max((int)a, (int)b); }) },
            // { Math.members[42].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            //     => { return a is null || b is null ? null : System.Math.Min(Convert.ToDouble(a), Convert.ToDouble(b)); }) },
            // { Math.members[43].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            //     => { return System.Math.Min(Convert.ToDouble(a), Convert.ToDouble(b)); }) },
            // { Math.members[44].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            //     => { return a is null || b is null ? null : System.Math.Min((int)a, (int)b); }) },
            // { Math.members[45].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            //     => { return System.Math.Min((int)a, (int)b); }) },
            // { Math.members[46].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            //     => { return a is null || b is null ? null : System.Math.Pow(Convert.ToDouble(a), Convert.ToDouble(b)); }) },
            // { Math.members[47].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            //     => { return System.Math.Pow(Convert.ToDouble(a), Convert.ToDouble(b)); }) },
            // { Math.members[48].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            //     => { return a is null ? null : System.Math.Round(Convert.ToDouble(a)); }) },
            // { Math.members[49].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            //     => { return System.Math.Round(Convert.ToDouble(a)); }) },
            // { Math.members[50].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            //     => { return a is null ? null : System.Math.Sin(Convert.ToDouble(a)); }) },
            // { Math.members[51].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            //     => { return System.Math.Sin(Convert.ToDouble(a)); }) },
            // { Math.members[52].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            //     => { return a is null ? null : System.Math.Sinh(Convert.ToDouble(a)); }) },
            // { Math.members[53].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            //     => { return System.Math.Sinh(Convert.ToDouble(a)); }) },
            // { Math.members[54].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            //     => { return a is null ? null : System.Math.Sqrt(Convert.ToDouble(a)); }) },
            // { Math.members[55].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            //     => { return System.Math.Sqrt(Convert.ToDouble(a)); }) },
            // { Math.members[56].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            //     => { return a is null ? null : System.Math.Tan(Convert.ToDouble(a)); }) },
            // { Math.members[57].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            //     => { return System.Math.Tan(Convert.ToDouble(a)); }) },
            // { Math.members[58].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            //     => { return a is null ? null : System.Math.Tanh(Convert.ToDouble(a)); }) },
            // { Math.members[59].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            //     => { return System.Math.Tanh(Convert.ToDouble(a)); }) },
            // { Math.members[60].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            //     => { return a is null ? null : System.Math.Truncate(Convert.ToDouble(a)); }) },
            // { Math.members[61].GetHashCode(), new Func<object, object, object, object>((a, b, c)
            //     => { return System.Math.Truncate(Convert.ToDouble(a)); }) },
        };
    }
}
