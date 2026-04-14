using System;
using System.Collections.Generic;
using System.Threading;
using Buckle.CodeAnalysis.Symbols;
using static Buckle.Libraries.LibraryHelpers;

namespace Buckle.Libraries;

internal static class StandardLibrary {
    private static SynthesizedFinishedNamedTypeSymbol _lazyDirectory;
    private static SynthesizedFinishedNamedTypeSymbol _lazyFile;
    private static SynthesizedFinishedNamedTypeSymbol _lazyConsole;
    private static SynthesizedFinishedNamedTypeSymbol _lazyMath;
    private static SynthesizedFinishedNamedTypeSymbol _lazyLowLevel;
    private static SynthesizedFinishedNamedTypeSymbol _lazyTime;
    private static SynthesizedFinishedNamedTypeSymbol _lazyRandom;
    private static SynthesizedFinishedNamedTypeSymbol _lazyString;
    private static SynthesizedFinishedNamedTypeSymbol _lazyInt;
    private static SynthesizedFinishedNamedTypeSymbol _lazyCallingConvention;
    private static Dictionary<string, Func<object, object, object, object>> _lazyEvaluatorMap;

    internal static SynthesizedFinishedNamedTypeSymbol LowLevel {
        get {
            if (_lazyLowLevel is null)
                Interlocked.CompareExchange(ref _lazyLowLevel, GenerateLowLevel(), null);

            return _lazyLowLevel;
        }
    }

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

    internal static SynthesizedFinishedNamedTypeSymbol Time {
        get {
            if (_lazyTime is null)
                Interlocked.CompareExchange(ref _lazyTime, GenerateTime(), null);

            return _lazyTime;
        }
    }

    internal static SynthesizedFinishedNamedTypeSymbol Random {
        get {
            if (_lazyRandom is null)
                Interlocked.CompareExchange(ref _lazyRandom, GenerateRandom(), null);

            return _lazyRandom;
        }
    }

    internal static SynthesizedFinishedNamedTypeSymbol String {
        get {
            if (_lazyString is null)
                Interlocked.CompareExchange(ref _lazyString, GenerateString(), null);

            return _lazyString;
        }
    }

    internal static SynthesizedFinishedNamedTypeSymbol Int {
        get {
            if (_lazyInt is null)
                Interlocked.CompareExchange(ref _lazyInt, GenerateInt(), null);

            return _lazyInt;
        }
    }

    internal static SynthesizedFinishedNamedTypeSymbol CallingConvention {
        get {
            if (_lazyCallingConvention is null)
                Interlocked.CompareExchange(ref _lazyCallingConvention, GenerateCallingConvention(), null);

            return _lazyCallingConvention;
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
        yield return Directory;
        yield return File;
        yield return Console;
        yield return Math;
        yield return LowLevel;
        yield return Time;
        yield return Random;
        yield return String;
        yield return Int;
        yield return CallingConvention;
    }

    internal static MethodSymbol GetPowerMethod(bool isLifted, bool isInt) {
        return (MethodSymbol)Math.GetMembers("Pow")[(isLifted ? 0 : 1) + (isInt ? 2 : 0)];
    }

    private static SynthesizedFinishedNamedTypeSymbol GenerateRandom() {
        return StaticClass("Random", [
            StaticMethod("RandInt", SpecialType.Int, [("max", SpecialType.Int, true)]),
            StaticMethod("Random", SpecialType.Decimal),
        ]);
    }

    private static SynthesizedFinishedNamedTypeSymbol GenerateString() {
        return StaticClass("String", [
            StaticMethod("Split", StringArray, [("text", SpecialType.String), ("separator", SpecialType.String)]),
            StaticMethod("Ascii", SpecialType.Int, true, [("chr", SpecialType.String)]),
            StaticMethod("Char", SpecialType.String, [("ascii", SpecialType.Int)]),
            StaticMethod("Length", SpecialType.Int, [("str", SpecialType.String)]),
            StaticMethod("IsNullOrWhiteSpace", SpecialType.Bool, [("str", SpecialType.String, true)]),
            StaticMethod("IsNullOrWhiteSpace", SpecialType.Bool, [("chr", SpecialType.Char, true)]),
            StaticMethod("IsDigit", SpecialType.Bool, [("chr", SpecialType.Char, true)]),
            StaticMethod("Substring", SpecialType.String, true, [("text", SpecialType.String, true), ("start", SpecialType.Int, true), ("length", SpecialType.Int, true)]),
        ]);
    }

    private static SynthesizedFinishedNamedTypeSymbol GenerateInt() {
        return StaticClass("Int", [
            StaticMethod("Parse", SpecialType.Int, true, [("text", SpecialType.String, true)]),
        ]);
    }

    private static SynthesizedFinishedNamedTypeSymbol GenerateCallingConvention() {
        return StaticClass("CallingConvention", [
            ConstExprField("Winapi", SpecialType.UInt32, (uint)1),
            ConstExprField("Cdecl", SpecialType.UInt32, (uint)2),
        ]);
    }

    private static SynthesizedFinishedNamedTypeSymbol GenerateTime() {
        return StaticClass("Time", [
            StaticMethod("Now", SpecialType.Int),
            StaticMethod("Sleep", SpecialType.Void, [("milliseconds", SpecialType.Int)]),
        ]);
    }

    private static SynthesizedFinishedNamedTypeSymbol GenerateLowLevel() {
        var lengthT = new SynthesizedTemplateParameterSymbol(
            null,
            new TypeWithAnnotations(CorLibrary.GetSpecialType(SpecialType.Type)),
            0
        );

        var length = new SynthesizedTemplateMethodSymbol(
            "Length",
            null,
            new TypeWithAnnotations(CorLibrary.GetSpecialType(SpecialType.Int)),
            [lengthT],
            [SynthesizedParameterSymbol.Create(null, new TypeWithAnnotations(lengthT), 0, RefKind.None, "arr")],
            MethodKind.Ordinary,
            CodeAnalysis.DeclarationModifiers.Static
        );

        var sortT = new SynthesizedTemplateParameterSymbol(
            null,
            new TypeWithAnnotations(CorLibrary.GetSpecialType(SpecialType.Type)),
            0
        );

        var sort = new SynthesizedTemplateMethodSymbol(
            "Sort",
            null,
            new TypeWithAnnotations(CorLibrary.GetSpecialType(SpecialType.Void)),
            [sortT],
            [SynthesizedParameterSymbol.Create(null, new TypeWithAnnotations(sortT), 0, RefKind.None, "arr")],
            MethodKind.Ordinary,
            CodeAnalysis.DeclarationModifiers.Static
        );

        var sizeOfT = new SynthesizedTemplateParameterSymbol(
            null,
            new TypeWithAnnotations(CorLibrary.GetSpecialType(SpecialType.Type)),
            0
        );

        var sizeOf = new SynthesizedTemplateMethodSymbol(
            "SizeOf",
            null,
            new TypeWithAnnotations(CorLibrary.GetSpecialType(SpecialType.Int32)),
            [sizeOfT],
            [],
            MethodKind.Ordinary,
            CodeAnalysis.DeclarationModifiers.Static
        );

        var createLPCSTR =
            new SynthesizedFinishedMethodSymbol(
                new SynthesizedSimpleOrdinaryMethodSymbol(
                    "CreateLPCSTR",
                    new TypeWithAnnotations(new PointerTypeSymbol(new TypeWithAnnotations(CorLibrary.GetSpecialType(SpecialType.UInt8)))),
                    RefKind.None,
                    CodeAnalysis.DeclarationModifiers.Public | CodeAnalysis.DeclarationModifiers.Static
                ),
            null,
            [SynthesizedParameterSymbol.Create(null, new TypeWithAnnotations(CorLibrary.GetSpecialType(SpecialType.String)), 0, RefKind.None, "str")]
        );

        var createLPCWSTR =
            new SynthesizedFinishedMethodSymbol(
                new SynthesizedSimpleOrdinaryMethodSymbol(
                    "CreateLPCWSTR",
                    new TypeWithAnnotations(new PointerTypeSymbol(new TypeWithAnnotations(CorLibrary.GetSpecialType(SpecialType.Char)))),
                    RefKind.None,
                    CodeAnalysis.DeclarationModifiers.Public | CodeAnalysis.DeclarationModifiers.Static
                ),
            null,
            [SynthesizedParameterSymbol.Create(null, new TypeWithAnnotations(CorLibrary.GetSpecialType(SpecialType.String)), 0, RefKind.None, "str")]
        );

        var freeLPCSTR =
            new SynthesizedFinishedMethodSymbol(
                new SynthesizedSimpleOrdinaryMethodSymbol(
                    "FreeLPCSTR",
                    new TypeWithAnnotations(CorLibrary.GetSpecialType(SpecialType.Void)),
                    RefKind.None,
                    CodeAnalysis.DeclarationModifiers.Public | CodeAnalysis.DeclarationModifiers.Static
                ),
                null,
                [SynthesizedParameterSymbol.Create(null, new TypeWithAnnotations(new PointerTypeSymbol(new TypeWithAnnotations(CorLibrary.GetSpecialType(SpecialType.UInt8)))), 0, RefKind.None, "str")]
        );

        var freeLPCWSTR =
            new SynthesizedFinishedMethodSymbol(
                new SynthesizedSimpleOrdinaryMethodSymbol(
                    "FreeLPCWSTR",
                    new TypeWithAnnotations(CorLibrary.GetSpecialType(SpecialType.Void)),
                    RefKind.None,
                    CodeAnalysis.DeclarationModifiers.Public | CodeAnalysis.DeclarationModifiers.Static
                ),
                null,
                [SynthesizedParameterSymbol.Create(null, new TypeWithAnnotations(new PointerTypeSymbol(new TypeWithAnnotations(CorLibrary.GetSpecialType(SpecialType.Char)))), 0, RefKind.None, "str")]
        );

        var readLPCSTR =
            new SynthesizedFinishedMethodSymbol(
                new SynthesizedSimpleOrdinaryMethodSymbol(
                    "ReadLPCSTR",
                    new TypeWithAnnotations(CorLibrary.GetSpecialType(SpecialType.String)),
                    RefKind.None,
                    CodeAnalysis.DeclarationModifiers.Public | CodeAnalysis.DeclarationModifiers.Static
                ),
                null,
                [SynthesizedParameterSymbol.Create(null, new TypeWithAnnotations(new PointerTypeSymbol(new TypeWithAnnotations(CorLibrary.GetSpecialType(SpecialType.UInt8)))), 0, RefKind.None, "ptr")]
        );

        var readLPCWSTR =
            new SynthesizedFinishedMethodSymbol(
                new SynthesizedSimpleOrdinaryMethodSymbol(
                    "ReadLPCWSTR",
                    new TypeWithAnnotations(CorLibrary.GetSpecialType(SpecialType.String)),
                    RefKind.None,
                    CodeAnalysis.DeclarationModifiers.Public | CodeAnalysis.DeclarationModifiers.Static
                ),
                null,
                [SynthesizedParameterSymbol.Create(null, new TypeWithAnnotations(new PointerTypeSymbol(new TypeWithAnnotations(CorLibrary.GetSpecialType(SpecialType.Char)))), 0, RefKind.None, "ptr")]
        );

        var getGCPtr =
            new SynthesizedFinishedMethodSymbol(
                new SynthesizedSimpleOrdinaryMethodSymbol(
                    "GetGCPtr",
                    new TypeWithAnnotations(new PointerTypeSymbol(new TypeWithAnnotations(CorLibrary.GetSpecialType(SpecialType.Void)))),
                    RefKind.None,
                    CodeAnalysis.DeclarationModifiers.Public | CodeAnalysis.DeclarationModifiers.Static
                ),
                null,
                [SynthesizedParameterSymbol.Create(null, new TypeWithAnnotations(CorLibrary.GetSpecialType(SpecialType.Object)), 0, RefKind.None, "obj")]
        );

        var freeGCHandle =
            new SynthesizedFinishedMethodSymbol(
                new SynthesizedSimpleOrdinaryMethodSymbol(
                    "FreeGCHandle",
                    new TypeWithAnnotations(CorLibrary.GetSpecialType(SpecialType.Void)),
                    RefKind.None,
                    CodeAnalysis.DeclarationModifiers.Public | CodeAnalysis.DeclarationModifiers.Static
                ),
                null,
                [SynthesizedParameterSymbol.Create(null, new TypeWithAnnotations(new PointerTypeSymbol(new TypeWithAnnotations(CorLibrary.GetSpecialType(SpecialType.Void)))), 0, RefKind.None, "ptr")]
        );

        var getObject =
            new SynthesizedFinishedMethodSymbol(
                new SynthesizedSimpleOrdinaryMethodSymbol(
                    "GetObject",
                    new TypeWithAnnotations(CorLibrary.GetSpecialType(SpecialType.Object)),
                    RefKind.None,
                    CodeAnalysis.DeclarationModifiers.Public | CodeAnalysis.DeclarationModifiers.Static
                ),
                null,
                [SynthesizedParameterSymbol.Create(null, new TypeWithAnnotations(new PointerTypeSymbol(new TypeWithAnnotations(CorLibrary.GetSpecialType(SpecialType.Void)))), 0, RefKind.None, "ptr")]
        );

        return StaticClass("LowLevel", [
            StaticMethod("GetHashCode", SpecialType.Int, [("object", SpecialType.Object)]),
            StaticMethod("GetTypeName", SpecialType.String, [("object", SpecialType.Object)]),
            length,
            sort,
            sizeOf,
            StaticMethod("ThrowNullConditionException", SpecialType.Void),
            createLPCSTR,
            createLPCWSTR,
            freeLPCWSTR,
            freeLPCSTR,
            readLPCSTR,
            readLPCWSTR,
            getGCPtr,
            freeGCHandle,
            getObject,
        ]);
    }

    private static SynthesizedFinishedNamedTypeSymbol GenerateDirectory() {
        return StaticClass("Directory", [
            StaticMethod("Create", SpecialType.Void, [("path", SpecialType.String)]),
            StaticMethod("Delete", SpecialType.Void, [("path", SpecialType.String)]),
            StaticMethod("Exists", SpecialType.Bool, [("path", SpecialType.String)]),
            StaticMethod("GetCurrentDirectory", SpecialType.String),
            // StaticMethod("GetDirectories", StringList, [("path", SpecialType.String)]),
            // StaticMethod("GetFiles", StringList, [("path", SpecialType.String)]),
        ]);
    }

    private static SynthesizedFinishedNamedTypeSymbol GenerateFile() {
        return StaticClass("File", [
            // StaticMethod("AppendLines", SpecialType.Void, [("fileName", SpecialType.String), ("lines", StringList)]),
            StaticMethod("AppendText", SpecialType.Void, [("fileName", SpecialType.String), ("text", SpecialType.String)]),
            StaticMethod("Create", SpecialType.Void, [("path", SpecialType.String)]),
            StaticMethod("Copy", SpecialType.Void, [("sourceFileName", SpecialType.String), ("destinationFileName", SpecialType.String)]),
            StaticMethod("Delete", SpecialType.Void, [("path", SpecialType.String)]),
            StaticMethod("Exists", SpecialType.Bool, [("path", SpecialType.String)]),
            // StaticMethod("ReadLines", StringList, [("fileName", SpecialType.String)]),
            StaticMethod("ReadText", SpecialType.String, true, [("fileName", SpecialType.String)]),
            // StaticMethod("WriteLines", SpecialType.Void, [("fileName", SpecialType.String), ("lines", StringList)]),
            StaticMethod("WriteText", SpecialType.Void, [("fileName", SpecialType.String), ("text", SpecialType.String)]),
        ]);
    }

    private static SynthesizedFinishedNamedTypeSymbol GenerateConsole() {
        return StaticClass("Console", [
            StaticClass("Color", [
                ConstExprField("Black", SpecialType.Int, 0L),
                ConstExprField("DarkBlue", SpecialType.Int, 1L),
                ConstExprField("DarkGreen", SpecialType.Int, 2L),
                ConstExprField("DarkCyan", SpecialType.Int, 3L),
                ConstExprField("DarkRed", SpecialType.Int, 4L),
                ConstExprField("DarkMagenta", SpecialType.Int, 5L),
                ConstExprField("DarkYellow", SpecialType.Int, 6L),
                ConstExprField("Gray", SpecialType.Int, 7L),
                ConstExprField("DarkGray", SpecialType.Int, 8L),
                ConstExprField("Blue", SpecialType.Int, 9L),
                ConstExprField("Green", SpecialType.Int, 10L),
                ConstExprField("Cyan", SpecialType.Int, 11L),
                ConstExprField("Red", SpecialType.Int, 12L),
                ConstExprField("Magenta", SpecialType.Int, 13L),
                ConstExprField("Yellow", SpecialType.Int, 14L),
                ConstExprField("White", SpecialType.Int, 15L)
            ]),
            StaticMethod("Clear", SpecialType.Void),
            StaticMethod("GetWidth", SpecialType.Int),
            StaticMethod("GetHeight", SpecialType.Int),
            StaticMethod("Input", SpecialType.String),
            StaticMethod("PrintLine", SpecialType.Void),
            StaticMethod("PrintLine", SpecialType.Void, [("message", SpecialType.String, true)]),
            StaticMethod("PrintLine", SpecialType.Void, [("value", SpecialType.Any, true)]),
            StaticMethod("PrintLine", SpecialType.Void, [("object", SpecialType.Object, true)]),
            StaticMethod("PrintLine", SpecialType.Void, [("chars", CharArray, true)]),
            StaticMethod("Print", SpecialType.Void, [("message", SpecialType.String, true)]),
            StaticMethod("Print", SpecialType.Void, [("value", SpecialType.Any, true)]),
            StaticMethod("Print", SpecialType.Void, [("object", SpecialType.Object, true)]),
            StaticMethod("Print", SpecialType.Void, [("chars", CharArray, true)]),
            StaticMethod("ResetColor", SpecialType.Void),
            StaticMethod("SetForegroundColor", SpecialType.Void, [("color", SpecialType.Int)]),
            StaticMethod("SetBackgroundColor", SpecialType.Void, [("color", SpecialType.Int)]),
            StaticMethod("SetCursorPosition", SpecialType.Void, [("left", SpecialType.Int, true), ("top", SpecialType.Int, true)]),
            StaticMethod("SetCursorVisibility", SpecialType.Void, [("visible", SpecialType.Bool)]),
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
            StaticMethod("Pow", SpecialType.Int, true, [("x", SpecialType.Int, true), ("y", SpecialType.Int, true)]),
            StaticMethod("Pow", SpecialType.Int, [("x", SpecialType.Int), ("y", SpecialType.Int)]),
            StaticMethod("Round", SpecialType.Decimal, true, [("value", SpecialType.Decimal, true)]),
            StaticMethod("Round", SpecialType.Decimal, [("value", SpecialType.Decimal)]),
            StaticMethod("Sign", SpecialType.Int, [("value", SpecialType.Decimal)]),
            StaticMethod("Sign", SpecialType.Int, true, [("value", SpecialType.Decimal, true)]),
            StaticMethod("Sign", SpecialType.Int, [("value", SpecialType.Int)]),
            StaticMethod("Sign", SpecialType.Int, true, [("value", SpecialType.Int, true)]),
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
            StaticMethod("DegToRad", SpecialType.Decimal, true, [("degrees", SpecialType.Decimal, true)]),
            StaticMethod("DegToRad", SpecialType.Decimal, [("degrees", SpecialType.Decimal)]),
            StaticMethod("RadToDeg", SpecialType.Decimal, true, [("radians", SpecialType.Decimal, true)]),
            StaticMethod("RadToDeg", SpecialType.Decimal, [("radians", SpecialType.Decimal)]),
        ]);
    }

    private static Dictionary<string, Func<object, object, object, object>> GenerateEvaluatorMap() {
        return new Dictionary<string, Func<object, object, object, object>>() {
            { "Console_Clear", new Func<object, object, object, object>((a, b, c)
                => { if (!System.Console.IsOutputRedirected) System.Console.Clear(); return null; }) },
            { "Console_GetWidth", new Func<object, object, object, object>((a, b, c)
                => { if (!System.Console.IsOutputRedirected) return System.Console.WindowWidth; return null; }) },
            { "Console_GetHeight", new Func<object, object, object, object>((a, b, c)
                => { if (!System.Console.IsOutputRedirected) return System.Console.WindowHeight; return null; }) },
            { "Console_Input", new Func<object, object, object, object>((a, b, c)
                => { if (!System.Console.IsOutputRedirected) return System.Console.ReadLine(); return null; }) },
            { "Console_PrintLine", new Func<object, object, object, object>((a, b, c)
                => { if (!System.Console.IsOutputRedirected) System.Console.WriteLine(); return null; }) },
            { "Console_PrintLine_S?", new Func<object, object, object, object>((a, b, c)
                => { if (!System.Console.IsOutputRedirected) System.Console.WriteLine(a); return null; }) },
            { "Console_PrintLine_A?", new Func<object, object, object, object>((a, b, c)
                => { if (!System.Console.IsOutputRedirected) System.Console.WriteLine(a); return null; }) },
            { "Console_PrintLine_O?", new Func<object, object, object, object>((a, b, c)
                => { if (!System.Console.IsOutputRedirected) System.Console.WriteLine(a); return null; }) },
            { "Console_PrintLine_[?", new Func<object, object, object, object>((a, b, c)
                => { if (!System.Console.IsOutputRedirected) System.Console.WriteLine(Array.ConvertAll((object[])a, i => (char)i)); return null; }) },
            { "Console_Print_S?", new Func<object, object, object, object>((a, b, c)
                => { if (!System.Console.IsOutputRedirected) System.Console.Write(a); return null; }) },
            { "Console_Print_A?", new Func<object, object, object, object>((a, b, c)
                => { if (!System.Console.IsOutputRedirected) System.Console.Write(a); return null; }) },
            { "Console_Print_O?", new Func<object, object, object, object>((a, b, c)
                => { if (!System.Console.IsOutputRedirected) System.Console.Write(a); return null; }) },
            { "Console_Print_[?", new Func<object, object, object, object>((a, b, c)
                => { if (!System.Console.IsOutputRedirected) System.Console.Write(Array.ConvertAll((object[])a, i => (char)i)); return null; }) },
            { "Console_ResetColor", new Func<object, object, object, object>((a, b, c)
                => { if (!System.Console.IsOutputRedirected) System.Console.ResetColor(); return null; }) },
            { "Console_SetForegroundColor_I", new Func<object, object, object, object>((a, b, c)
                => { if (!System.Console.IsOutputRedirected) System.Console.ForegroundColor = (ConsoleColor)(long)a; return null; }) },
            { "Console_SetBackgroundColor_I", new Func<object, object, object, object>((a, b, c)
                => { if (!System.Console.IsOutputRedirected) System.Console.BackgroundColor = (ConsoleColor)(long)a; return null; }) },
            { "Console_SetCursorPosition_I?I?", new Func<object, object, object, object>((a, b, c)
                => { if (!System.Console.IsOutputRedirected) { System.Console.SetCursorPosition(a is null ? System.Console.CursorLeft : Convert.ToInt32(a), b is null ? System.Console.CursorTop : Convert.ToInt32(b)); } return null; }) },
            { "Console_SetCursorVisibility_B", new Func<object, object, object, object>((a, b, c)
                => { if (!System.Console.IsOutputRedirected) System.Console.CursorVisible = Convert.ToBoolean(a); return null;}) },
            { "Directory_Create_S", new Func<object, object, object, object>((a, b, c)
                => { System.IO.Directory.CreateDirectory((string)a); return null; }) },
            { "Directory_Delete_S", new Func<object, object, object, object>((a, b, c)
                => { System.IO.Directory.Delete((string)a, true); return null; }) },
            { "Directory_Exists_S", new Func<object, object, object, object>((a, b, c)
                => { return System.IO.Directory.Exists((string)a); }) },
            { "Directory_GetCurrentDirectory", new Func<object, object, object, object>((a, b, c)
                => { return System.IO.Directory.GetCurrentDirectory(); }) },
            { "File_AppendText_SS", new Func<object, object, object, object>((a, b, c)
                => { System.IO.File.AppendAllText((string)a, (string)b); return null; }) },
            { "File_Create_S", new Func<object, object, object, object>((a, b, c)
                => { System.IO.File.Create((string)a); return null; }) },
            { "File_Copy_SS", new Func<object, object, object, object>((a, b, c)
                => { System.IO.File.Copy((string)a, (string)b); return null; }) },
            { "File_Delete_S", new Func<object, object, object, object>((a, b, c)
                => { System.IO.File.Delete((string)a); return null; }) },
            { "File_Exists_S", new Func<object, object, object, object>((a, b, c)
                => { return System.IO.File.Exists((string)a); }) },
            { "File_ReadText_S", new Func<object, object, object, object>((a, b, c)
                => { return System.IO.File.ReadAllText((string)a); }) },
            { "File_WriteText_SS", new Func<object, object, object, object>((a, b, c)
                => { System.IO.File.WriteAllText((string)a, (string)b); return null; }) },
            { "Math_Abs_D?", new Func<object, object, object, object>((a, b, c)
                => { return a is null ? null : System.Math.Abs(Convert.ToDouble(a)); }) },
            { "Math_Abs_D", new Func<object, object, object, object>((a, b, c)
                => { return System.Math.Abs(Convert.ToDouble(a)); }) },
            { "Math_Abs_I?", new Func<object, object, object, object>((a, b, c)
                => { return a is null ? null : System.Math.Abs((long)a); }) },
            { "Math_Abs_I", new Func<object, object, object, object>((a, b, c)
                => { return System.Math.Abs((long)a); }) },
            { "Math_Acos_D?", new Func<object, object, object, object>((a, b, c)
                => { return a is null ? null : System.Math.Acos(Convert.ToDouble(a)); }) },
            { "Math_Acos_D", new Func<object, object, object, object>((a, b, c)
                => { return System.Math.Acos(Convert.ToDouble(a)); }) },
            { "Math_Acosh_D?", new Func<object, object, object, object>((a, b, c)
                => { return a is null ? null : System.Math.Acosh(Convert.ToDouble(a)); }) },
            { "Math_Acosh_D", new Func<object, object, object, object>((a, b, c)
                => { return System.Math.Acosh(Convert.ToDouble(a)); }) },
            { "Math_Asin_D?", new Func<object, object, object, object>((a, b, c)
                => { return a is null ? null : System.Math.Asin(Convert.ToDouble(a)); }) },
            { "Math_Asin_D", new Func<object, object, object, object>((a, b, c)
                => { return System.Math.Asin(Convert.ToDouble(a)); }) },
            { "Math_Asinh_D?", new Func<object, object, object, object>((a, b, c)
                => { return a is null ? null : System.Math.Asinh(Convert.ToDouble(a)); }) },
            { "Math_Asinh_D", new Func<object, object, object, object>((a, b, c)
                => { return System.Math.Asinh(Convert.ToDouble(a)); }) },
            { "Math_Atan_D?", new Func<object, object, object, object>((a, b, c)
                => { return a is null ? null : System.Math.Atan(Convert.ToDouble(a)); }) },
            { "Math_Atan_D", new Func<object, object, object, object>((a, b, c)
                => { return System.Math.Atan(Convert.ToDouble(a)); }) },
            { "Math_Atanh_D?", new Func<object, object, object, object>((a, b, c)
                => { return a is null ? null : System.Math.Atanh(Convert.ToDouble(a)); }) },
            { "Math_Atanh_D", new Func<object, object, object, object>((a, b, c)
                => { return System.Math.Atanh(Convert.ToDouble(a)); }) },
            { "Math_Ceiling_D?", new Func<object, object, object, object>((a, b, c)
                => { return a is null ? null : System.Math.Ceiling(Convert.ToDouble(a)); }) },
            { "Math_Ceiling_D", new Func<object, object, object, object>((a, b, c)
                => { return System.Math.Ceiling(Convert.ToDouble(a)); }) },
            { "Math_Clamp_D?D?D?", new Func<object, object, object, object>((a, b, c)
                => { return (a is null || b is null || c is null) ? null : System.Math.Clamp(Convert.ToDouble(a), Convert.ToDouble(b), Convert.ToDouble(c)); }) },
            { "Math_Clamp_DDD", new Func<object, object, object, object>((a, b, c)
                => { return System.Math.Clamp(Convert.ToDouble(a), Convert.ToDouble(b), Convert.ToDouble(c)); }) },
            { "Math_Clamp_I?I?I?", new Func<object, object, object, object>((a, b, c)
                => { return (a is null || b is null || c is null) ? null : System.Math.Clamp((long)a, (long)b, (long)c); }) },
            { "Math_Clamp_III", new Func<object, object, object, object>((a, b, c)
                => { return System.Math.Clamp((long)a, (long)b, (long)c); }) },
            { "Math_Cos_D?", new Func<object, object, object, object>((a, b, c)
                => { return a is null ? null : System.Math.Cos(Convert.ToDouble(a)); }) },
            { "Math_Cos_D", new Func<object, object, object, object>((a, b, c)
                => { return System.Math.Cos(Convert.ToDouble(a)); }) },
            { "Math_Cosh_D?", new Func<object, object, object, object>((a, b, c)
                => { return a is null ? null : System.Math.Cosh(Convert.ToDouble(a)); }) },
            { "Math_Cosh_D", new Func<object, object, object, object>((a, b, c)
                => { return System.Math.Cosh(Convert.ToDouble(a)); }) },
            { "Math_Exp_D?", new Func<object, object, object, object>((a, b, c)
                => { return a is null ? null : System.Math.Exp(Convert.ToDouble(a)); }) },
            { "Math_Exp_D", new Func<object, object, object, object>((a, b, c)
                => { return System.Math.Exp(Convert.ToDouble(a)); }) },
            { "Math_Floor_D?", new Func<object, object, object, object>((a, b, c)
                => { return a is null ? null : System.Math.Floor(Convert.ToDouble(a)); }) },
            { "Math_Floor_D", new Func<object, object, object, object>((a, b, c)
                => { return System.Math.Floor(Convert.ToDouble(a)); }) },
            { "Math_Lerp_D?D?D?", new Func<object, object, object, object>((a, b, c)
                => { if (a is null || b is null || c is null) return null; var rate = Convert.ToDouble(c); var start = Convert.ToDouble(a); return start + rate * (Convert.ToDouble(b) - start); }) },
            { "Math_Lerp_DDD", new Func<object, object, object, object>((a, b, c)
                => { var rate = Convert.ToDouble(c); return Convert.ToDouble(a) * (1 - rate) + Convert.ToDouble(b) * rate; }) },
            { "Math_Log_D?D?", new Func<object, object, object, object>((a, b, c)
                => { return a is null || b is null ? null : System.Math.Log(Convert.ToDouble(a), Convert.ToDouble(b)); }) },
            { "Math_Log_DD", new Func<object, object, object, object>((a, b, c)
                => { return System.Math.Log(Convert.ToDouble(a), Convert.ToDouble(b)); }) },
            { "Math_Log_D?", new Func<object, object, object, object>((a, b, c)
                => { return a is null ? null : System.Math.Log(Convert.ToDouble(a)); }) },
            { "Math_Log_D", new Func<object, object, object, object>((a, b, c)
                => { return System.Math.Log(Convert.ToDouble(a)); }) },
            { "Math_Max_D?D?", new Func<object, object, object, object>((a, b, c)
                => { return a is null || b is null ? null : System.Math.Max(Convert.ToDouble(a), Convert.ToDouble(b)); }) },
            { "Math_Max_DD", new Func<object, object, object, object>((a, b, c)
                => { return System.Math.Max(Convert.ToDouble(a), Convert.ToDouble(b)); }) },
            { "Math_Max_I?I?", new Func<object, object, object, object>((a, b, c)
                => { return a is null || b is null ? null : System.Math.Max((long)a, (long)b); }) },
            { "Math_Max_II", new Func<object, object, object, object>((a, b, c)
                => { return System.Math.Max((long)a, (long)b); }) },
            { "Math_Min_D?D?", new Func<object, object, object, object>((a, b, c)
                => { return a is null || b is null ? null : System.Math.Min(Convert.ToDouble(a), Convert.ToDouble(b)); }) },
            { "Math_Min_DD", new Func<object, object, object, object>((a, b, c)
                => { return System.Math.Min(Convert.ToDouble(a), Convert.ToDouble(b)); }) },
            { "Math_Min_I?I?", new Func<object, object, object, object>((a, b, c)
                => { return a is null || b is null ? null : System.Math.Min((long)a, (long)b); }) },
            { "Math_Min_II", new Func<object, object, object, object>((a, b, c)
                => { return System.Math.Min((long)a, (long)b); }) },
            { "Math_Pow_D?D?", new Func<object, object, object, object>((a, b, c)
                => { return a is null || b is null ? null : System.Math.Pow(Convert.ToDouble(a), Convert.ToDouble(b)); }) },
            { "Math_Pow_DD", new Func<object, object, object, object>((a, b, c)
                => { return System.Math.Pow(Convert.ToDouble(a), Convert.ToDouble(b)); }) },
            { "Math_Pow_I?I?", new Func<object, object, object, object>((a, b, c)
                => { return a is null || b is null ? null : Convert.ToInt64(System.Math.Pow(Convert.ToDouble(a), Convert.ToDouble(b))); }) },
            { "Math_Pow_II", new Func<object, object, object, object>((a, b, c)
                => { return Convert.ToInt64(System.Math.Pow(Convert.ToDouble(a), Convert.ToDouble(b))); }) },
            { "Math_Round_D?", new Func<object, object, object, object>((a, b, c)
                => { return a is null ? null : System.Math.Round(Convert.ToDouble(a)); }) },
            { "Math_Round_D", new Func<object, object, object, object>((a, b, c)
                => { return System.Math.Round(Convert.ToDouble(a)); }) },
            { "Math_Sign_D?", new Func<object, object, object, object>((a, b, c)
                => { return a is null ? null : System.Math.Sign(Convert.ToDouble(a)); }) },
            { "Math_Sign_D", new Func<object, object, object, object>((a, b, c)
                => { return System.Math.Sign(Convert.ToDouble(a)); }) },
            { "Math_Sign_I?", new Func<object, object, object, object>((a, b, c)
                => { return a is null ? null : System.Math.Sign(Convert.ToInt64(a)); }) },
            { "Math_Sign_I", new Func<object, object, object, object>((a, b, c)
                => { return System.Math.Sign(Convert.ToInt64(a)); }) },
            { "Math_Sin_D?", new Func<object, object, object, object>((a, b, c)
                => { return a is null ? null : System.Math.Sin(Convert.ToDouble(a)); }) },
            { "Math_Sin_D", new Func<object, object, object, object>((a, b, c)
                => { return System.Math.Sin(Convert.ToDouble(a)); }) },
            { "Math_Sinh_D?", new Func<object, object, object, object>((a, b, c)
                => { return a is null ? null : System.Math.Sinh(Convert.ToDouble(a)); }) },
            { "Math_Sinh_D", new Func<object, object, object, object>((a, b, c)
                => { return System.Math.Sinh(Convert.ToDouble(a)); }) },
            { "Math_Sqrt_D?", new Func<object, object, object, object>((a, b, c)
                => { return a is null ? null : System.Math.Sqrt(Convert.ToDouble(a)); }) },
            { "Math_Sqrt_D", new Func<object, object, object, object>((a, b, c)
                => { return System.Math.Sqrt(Convert.ToDouble(a)); }) },
            { "Math_Tan_D?", new Func<object, object, object, object>((a, b, c)
                => { return a is null ? null : System.Math.Tan(Convert.ToDouble(a)); }) },
            { "Math_Tan_D", new Func<object, object, object, object>((a, b, c)
                => { return System.Math.Tan(Convert.ToDouble(a)); }) },
            { "Math_Tanh_D?", new Func<object, object, object, object>((a, b, c)
                => { return a is null ? null : System.Math.Tanh(Convert.ToDouble(a)); }) },
            { "Math_Tanh_D", new Func<object, object, object, object>((a, b, c)
                => { return System.Math.Tanh(Convert.ToDouble(a)); }) },
            { "Math_Truncate_D?", new Func<object, object, object, object>((a, b, c)
                => { return a is null ? null : System.Math.Truncate(Convert.ToDouble(a)); }) },
            { "Math_Truncate_D", new Func<object, object, object, object>((a, b, c)
                => { return System.Math.Truncate(Convert.ToDouble(a)); }) },
            { "Math_DegToRad_D?", new Func<object, object, object, object>((a, b, c)
                => { return a is null ? null : double.DegreesToRadians(Convert.ToDouble(a)); }) },
            { "Math_DegToRad_D", new Func<object, object, object, object>((a, b, c)
                => { return double.DegreesToRadians(Convert.ToDouble(a)); }) },
            { "Math_RadToDeg_D?", new Func<object, object, object, object>((a, b, c)
                => { return a is null ? null : double.RadiansToDegrees(Convert.ToDouble(a)); }) },
            { "Math_RadToDeg_D", new Func<object, object, object, object>((a, b, c)
                => { return double.RadiansToDegrees(Convert.ToDouble(a)); }) },
            { "Time_Now", new Func<object, object, object, object>((a, b, c)
                => { return DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond; }) },
            { "Time_Sleep_I", new Func<object, object, object, object>((a, b, c)
                => { Thread.Sleep(Convert.ToInt32(a)); return null; }) },
            { "String_Ascii_S", new Func<object, object, object, object>((a, b, c)
                => { return char.TryParse((string)a, out var result) ? (long)result : null; }) },
            { "String_Char_I", new Func<object, object, object, object>((a, b, c)
                => { return Convert.ToChar(a); }) },
            { "String_IsNullOrWhiteSpace_S?", new Func<object, object, object, object>((a, b, c)
                => { return string.IsNullOrWhiteSpace((string)a); }) },
            { "String_IsNullOrWhiteSpace_C?", new Func<object, object, object, object>((a, b, c)
                => { return a is null || char.IsWhiteSpace((char)a); }) },
            { "String_IsDigit_C?", new Func<object, object, object, object>((a, b, c)
                => { return a is not null && char.IsDigit((char)a); }) },
            { "String_Length_S", new Func<object, object, object, object>((a, b, c)
                => { return ((string)a).Length; }) },
            { "String_Substring_S?I?I?", new Func<object, object, object, object>((a, b, c)
                => { if (a is null) return null;
                     if (c is null) return ((string)a).Substring(b is null ? 0 : unchecked((int)(long)b));
                     return ((string)a).Substring(b is null ? 0 : unchecked((int)(long)b), unchecked((int)(long)c)); }) },
            { "Int_Parse_S?", new Func<object, object, object, object>((a, b, c)
                => { if (a is null) return null;
                     if (long.TryParse((string)a, out var result)) return result;
                     return null; }) },
        };
    }
}
