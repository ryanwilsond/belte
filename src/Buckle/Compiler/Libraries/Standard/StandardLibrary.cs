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

    // TODO Should probably reevaluate what to do about method implementations
    // The method map was efficient but very messy to setup
    // Will be some sort of map, but hopefully less messy (make sure to lock to avoid race conditions)
    // Easiest way is to just create unique string names? Then give SynthesizedFinishedMethodSymbol its own field
    // Set hasSpecialName to true? (Only do this after implemented what this field is actually for)
    // Call it `corName` or `stlName` or `standardLibraryName` or something
    // Could also just attach all of the map data (C# string and Evaluator function) directly on the method symbol to
    // Avoid needing to do a lookup. This is NOT less space efficient because we shouldn't be creating multiple copies
    // Of these symbols. Could then in the Evaluator just be like
    // "Is the ContainingSymbol Console, Math, etc.? Ok now I will as cast this to the Synthesized and invoke the
    // function"
    // Or "Is the ContainingSymbol Console, Math, etc.? Ok now I will lookup the map using the unique string name"
}
