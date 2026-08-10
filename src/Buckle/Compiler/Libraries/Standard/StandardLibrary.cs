using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Buckle.CodeAnalysis;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Utilities;
using static Buckle.Libraries.LibraryHelpers;

namespace Buckle.Libraries;

internal class StandardLibrary {
#if DEBUG
    private static int InstantiationCount = 0;
#endif

    private SynthesizedFinishedNamedTypeSymbol _lazyDirectory;
    private SynthesizedFinishedNamedTypeSymbol _lazyFile;
    private SynthesizedFinishedNamedTypeSymbol _lazyConsole;
    private SynthesizedFinishedNamedTypeSymbol _lazyMath;
    private SynthesizedFinishedNamedTypeSymbol _lazyLowLevel;
    private SynthesizedFinishedNamedTypeSymbol _lazyHashCode;
    private SynthesizedFinishedNamedTypeSymbol _lazyTime;
    private SynthesizedFinishedNamedTypeSymbol _lazyRandom;
    private SynthesizedFinishedNamedTypeSymbol _lazyString;
    private SynthesizedFinishedNamedTypeSymbol _lazyInt;
    private SynthesizedFinishedNamedTypeSymbol _lazyInt64;
    private SynthesizedFinishedNamedTypeSymbol _lazyInt32;
    private SynthesizedFinishedNamedTypeSymbol _lazyInt16;
    private SynthesizedFinishedNamedTypeSymbol _lazyInt8;
    private SynthesizedFinishedNamedTypeSymbol _lazyUInt64;
    private SynthesizedFinishedNamedTypeSymbol _lazyUInt32;
    private SynthesizedFinishedNamedTypeSymbol _lazyUInt16;
    private SynthesizedFinishedNamedTypeSymbol _lazyUInt8;
    private SynthesizedFinishedNamedTypeSymbol _lazyDecimal;
    private SynthesizedFinishedNamedTypeSymbol _lazyFloat64;
    private SynthesizedFinishedNamedTypeSymbol _lazyFloat32;
    private SynthesizedFinishedNamedTypeSymbol _lazyCallingConvention;
    private Dictionary<string, Func<object, object, object, object>> _lazyEvaluatorMap;
    private Dictionary<STLWellKnownMembers, MethodSymbol> _lazyWellKnownMembers;

    private readonly Compilation _compilation;

    internal StandardLibrary(Compilation compilation) {
        _compilation = compilation;

#if DEBUG
        InstantiationCount++;
#endif
    }

    internal SynthesizedFinishedNamedTypeSymbol LowLevel {
        get {
            if (_lazyLowLevel is null)
                Interlocked.CompareExchange(ref _lazyLowLevel, GenerateLowLevel(), null);

            return _lazyLowLevel;
        }
    }

    internal SynthesizedFinishedNamedTypeSymbol HashCode {
        get {
            if (_lazyHashCode is null)
                Interlocked.CompareExchange(ref _lazyHashCode, GenerateHashCode(), null);

            return _lazyHashCode;
        }
    }

    internal SynthesizedFinishedNamedTypeSymbol Directory {
        get {
            if (_lazyDirectory is null)
                Interlocked.CompareExchange(ref _lazyDirectory, GenerateDirectory(), null);

            return _lazyDirectory;
        }
    }

    internal SynthesizedFinishedNamedTypeSymbol File {
        get {
            if (_lazyFile is null)
                Interlocked.CompareExchange(ref _lazyFile, GenerateFile(), null);

            return _lazyFile;
        }
    }

    internal SynthesizedFinishedNamedTypeSymbol Console {
        get {
            if (_lazyConsole is null)
                Interlocked.CompareExchange(ref _lazyConsole, GenerateConsole(), null);

            return _lazyConsole;
        }
    }

    internal SynthesizedFinishedNamedTypeSymbol Math {
        get {
            if (_lazyMath is null)
                Interlocked.CompareExchange(ref _lazyMath, GenerateMath(), null);

            return _lazyMath;
        }
    }

    internal SynthesizedFinishedNamedTypeSymbol Time {
        get {
            if (_lazyTime is null)
                Interlocked.CompareExchange(ref _lazyTime, GenerateTime(), null);

            return _lazyTime;
        }
    }

    internal SynthesizedFinishedNamedTypeSymbol Random {
        get {
            if (_lazyRandom is null)
                Interlocked.CompareExchange(ref _lazyRandom, GenerateRandom(), null);

            return _lazyRandom;
        }
    }

    internal SynthesizedFinishedNamedTypeSymbol String {
        get {
            if (_lazyString is null)
                Interlocked.CompareExchange(ref _lazyString, GenerateString(), null);

            return _lazyString;
        }
    }

    internal SynthesizedFinishedNamedTypeSymbol Int {
        get {
            if (_lazyInt is null)
                Interlocked.CompareExchange(ref _lazyInt, GenerateInt(), null);

            return _lazyInt;
        }
    }

    internal SynthesizedFinishedNamedTypeSymbol Int64 {
        get {
            if (_lazyInt64 is null)
                Interlocked.CompareExchange(ref _lazyInt64, GenerateInt64(), null);

            return _lazyInt64;
        }
    }

    internal SynthesizedFinishedNamedTypeSymbol Int32 {
        get {
            if (_lazyInt32 is null)
                Interlocked.CompareExchange(ref _lazyInt32, GenerateInt32(), null);

            return _lazyInt32;
        }
    }

    internal SynthesizedFinishedNamedTypeSymbol Int16 {
        get {
            if (_lazyInt16 is null)
                Interlocked.CompareExchange(ref _lazyInt16, GenerateInt16(), null);

            return _lazyInt16;
        }
    }

    internal SynthesizedFinishedNamedTypeSymbol Int8 {
        get {
            if (_lazyInt8 is null)
                Interlocked.CompareExchange(ref _lazyInt8, GenerateInt8(), null);

            return _lazyInt8;
        }
    }

    internal SynthesizedFinishedNamedTypeSymbol UInt64 {
        get {
            if (_lazyUInt64 is null)
                Interlocked.CompareExchange(ref _lazyUInt64, GenerateUInt64(), null);

            return _lazyUInt64;
        }
    }

    internal SynthesizedFinishedNamedTypeSymbol UInt32 {
        get {
            if (_lazyUInt32 is null)
                Interlocked.CompareExchange(ref _lazyUInt32, GenerateUInt32(), null);

            return _lazyUInt32;
        }
    }

    internal SynthesizedFinishedNamedTypeSymbol UInt16 {
        get {
            if (_lazyUInt16 is null)
                Interlocked.CompareExchange(ref _lazyUInt16, GenerateUInt16(), null);

            return _lazyUInt16;
        }
    }

    internal SynthesizedFinishedNamedTypeSymbol UInt8 {
        get {
            if (_lazyUInt8 is null)
                Interlocked.CompareExchange(ref _lazyUInt8, GenerateUInt8(), null);

            return _lazyUInt8;
        }
    }

    internal SynthesizedFinishedNamedTypeSymbol Decimal {
        get {
            if (_lazyDecimal is null)
                Interlocked.CompareExchange(ref _lazyDecimal, GenerateDecimal(), null);

            return _lazyDecimal;
        }
    }

    internal SynthesizedFinishedNamedTypeSymbol Float64 {
        get {
            if (_lazyFloat64 is null)
                Interlocked.CompareExchange(ref _lazyFloat64, GenerateFloat64(), null);

            return _lazyFloat64;
        }
    }

    internal SynthesizedFinishedNamedTypeSymbol Float32 {
        get {
            if (_lazyFloat32 is null)
                Interlocked.CompareExchange(ref _lazyFloat32, GenerateFloat32(), null);

            return _lazyFloat32;
        }
    }

    internal SynthesizedFinishedNamedTypeSymbol CallingConvention {
        get {
            if (_lazyCallingConvention is null)
                Interlocked.CompareExchange(ref _lazyCallingConvention, GenerateCallingConvention(), null);

            return _lazyCallingConvention;
        }
    }

    internal Dictionary<string, Func<object, object, object, object>> EvaluatorMap {
        get {
            if (_lazyEvaluatorMap is null)
                Interlocked.CompareExchange(ref _lazyEvaluatorMap, GenerateEvaluatorMap(), null);

            return _lazyEvaluatorMap;
        }
    }

    private SpecialOrKnownType SVoid => _compilation.GetSpecialType(SpecialType.Void);
    private SpecialOrKnownType SString => _compilation.GetSpecialType(SpecialType.String);
    private SpecialOrKnownType SInt => _compilation.GetSpecialType(SpecialType.Int);
    private SpecialOrKnownType SBool => _compilation.GetSpecialType(SpecialType.Bool);
    private SpecialOrKnownType SDecimal => _compilation.GetSpecialType(SpecialType.Decimal);
    private SpecialOrKnownType SChar => _compilation.GetSpecialType(SpecialType.Char);
    private SpecialOrKnownType SInt64 => _compilation.GetSpecialType(SpecialType.Int64);
    private SpecialOrKnownType SInt32 => _compilation.GetSpecialType(SpecialType.Int32);
    private SpecialOrKnownType SInt16 => _compilation.GetSpecialType(SpecialType.Int16);
    private SpecialOrKnownType SInt8 => _compilation.GetSpecialType(SpecialType.Int8);
    private SpecialOrKnownType SUInt64 => _compilation.GetSpecialType(SpecialType.UInt64);
    private SpecialOrKnownType SUInt32 => _compilation.GetSpecialType(SpecialType.UInt32);
    private SpecialOrKnownType SUInt16 => _compilation.GetSpecialType(SpecialType.UInt16);
    private SpecialOrKnownType SUInt8 => _compilation.GetSpecialType(SpecialType.UInt8);
    private SpecialOrKnownType SFloat64 => _compilation.GetSpecialType(SpecialType.Float64);
    private SpecialOrKnownType SFloat32 => _compilation.GetSpecialType(SpecialType.Float32);
    private SpecialOrKnownType SAny => _compilation.GetSpecialType(SpecialType.Any);
    private SpecialOrKnownType SType => _compilation.GetSpecialType(SpecialType.Type);
    private SpecialOrKnownType SObject => _compilation.GetSpecialType(SpecialType.Object);

    private SpecialOrKnownType StringBuffer => GetStringBuffer(_compilation);
    private SpecialOrKnownType CharBuffer => GetCharBuffer(_compilation);

    internal IEnumerable<SynthesizedFinishedNamedTypeSymbol> GetTypes(bool reduced) {
        Debug.Assert(!reduced);
        yield return LowLevel;
        yield return CallingConvention;

        if (!reduced) {
            yield return Directory;
            yield return File;
            yield return Console;
            yield return Math;
            yield return Time;
            yield return Random;
            yield return String;
            yield return Int;
            yield return Decimal;
            yield return Float64;
            yield return Float32;
            yield return Int64;
            yield return Int32;
            yield return Int16;
            yield return Int8;
            yield return UInt64;
            yield return UInt32;
            yield return UInt16;
            yield return UInt8;
            yield return HashCode;
        }
    }

    internal MethodSymbol GetWellKnownMember(STLWellKnownMembers wellknownMember) {
        if (_lazyWellKnownMembers is null)
            Interlocked.CompareExchange(ref _lazyWellKnownMembers, GenerateWellKnownMembers(), null);

        return _lazyWellKnownMembers[wellknownMember];
    }

    private Dictionary<STLWellKnownMembers, MethodSymbol> GenerateWellKnownMembers() {
        return new Dictionary<STLWellKnownMembers, MethodSymbol>() {
            { STLWellKnownMembers.LowLevel_ThrowNullConditionException, (MethodSymbol)LowLevel.GetMembers("ThrowNullConditionException")[0] },
            { STLWellKnownMembers.LowLevel_BitCast, (MethodSymbol)LowLevel.GetMembers("BitCast")[0] },
            { STLWellKnownMembers.LowLevel_CreateLPCSTR, (MethodSymbol)LowLevel.GetMembers("CreateLPCSTR")[0] },
            { STLWellKnownMembers.LowLevel_FreeLPCSTR, (MethodSymbol)LowLevel.GetMembers("FreeLPCSTR")[0] },
            { STLWellKnownMembers.LowLevel_CreateLPCWSTR, (MethodSymbol)LowLevel.GetMembers("CreateLPCWSTR")[0] },
            { STLWellKnownMembers.LowLevel_FreeLPCWSTR, (MethodSymbol)LowLevel.GetMembers("FreeLPCWSTR")[0] },
            { STLWellKnownMembers.LowLevel_Length, (MethodSymbol)LowLevel.GetMembers("Length")[0] },
            { STLWellKnownMembers.String_Length, (MethodSymbol)String.GetMembers("Length")[0] },
        };
    }

    internal MethodSymbol GetPowerMethod(bool isLifted, bool isInt) {
        return (MethodSymbol)Math.GetMembers("Pow")[(isLifted ? 0 : 1) + (isInt ? 2 : 0)];
    }

    internal MethodSymbol GetMinMethod(bool isLifted, BinaryOperatorKind operandTypes) {
        var operandOffset = operandTypes switch {
            BinaryOperatorKind.Float64 => 0,
            BinaryOperatorKind.Float32 => 2,
            BinaryOperatorKind.Int64 => 4,
            BinaryOperatorKind.UInt64 => 6,
            BinaryOperatorKind.Int32 => 8,
            BinaryOperatorKind.UInt32 => 10,
            _ => throw ExceptionUtilities.UnexpectedValue(operandTypes)
        };

        return (MethodSymbol)Math.GetMembers("Min")[(isLifted ? 0 : 1) + operandOffset];
    }

    internal MethodSymbol GetMaxMethod(bool isLifted, BinaryOperatorKind operandTypes) {
        var operandOffset = operandTypes switch {
            BinaryOperatorKind.Float64 => 0,
            BinaryOperatorKind.Float32 => 2,
            BinaryOperatorKind.Int64 => 4,
            BinaryOperatorKind.UInt64 => 6,
            BinaryOperatorKind.Int32 => 8,
            BinaryOperatorKind.UInt32 => 10,
            _ => throw ExceptionUtilities.UnexpectedValue(operandTypes)
        };

        return (MethodSymbol)Math.GetMembers("Max")[(isLifted ? 0 : 1) + operandOffset];
    }

    internal MethodSymbol GetClampMethod(bool isLifted, SpecialType operandTypes) {
        var operandOffset = operandTypes switch {
            SpecialType.Float64 => 0,
            SpecialType.Float32 => 2,
            SpecialType.Int64 => 4,
            SpecialType.UInt64 => 6,
            SpecialType.Int32 => 8,
            SpecialType.UInt32 => 10,
            SpecialType.Int16 => 12,
            SpecialType.UInt16 => 14,
            SpecialType.Int8 => 16,
            SpecialType.UInt8 => 18,
            SpecialType.Char => 20,
            _ => throw ExceptionUtilities.UnexpectedValue(operandTypes)
        };

        return (MethodSymbol)Math.GetMembers("Clamp")[(isLifted ? 0 : 1) + operandOffset];
    }

    private SynthesizedFinishedNamedTypeSymbol GenerateRandom() {
        return StaticClass(_compilation, "Random", [
            StaticMethod("RandInt", SInt, [("max", SInt, true)]),
            StaticMethod("Random", SDecimal),
        ]);
    }

    private SynthesizedFinishedNamedTypeSymbol GenerateString() {
        return StaticClass(_compilation, "String", [
            StaticMethod("Split", StringBuffer, [("text", SString), ("separator", SString)]),
            StaticMethod("Ascii", SInt, true, [("chr", SString)]),
            StaticMethod("Char", SString, [("ascii", SInt)]),
            StaticMethod("Length", SInt, [("str", SString)]),
            StaticMethod("IsNullOrWhiteSpace", SBool, [("str", SString, true)]),
            StaticMethod("IsNullOrWhiteSpace", SBool, [("chr", SChar, true)]),
            StaticMethod("IsDigit", SBool, [("chr", SChar, true)]),
            StaticMethod("Substring", SString, [("text", SString, false), ("start", SInt, true), ("length", SInt, true)]),
            StaticMethod("IndexOf", SInt, [("text", SString), ("chr", SChar)]),
            StaticMethod("PadLeft", SString, [("text", SString), ("padding", SChar), ("totalWidth", SInt)]),
            StaticMethod("PadRight", SString, [("text", SString), ("padding", SChar), ("totalWidth", SInt)]),
            StaticMethod("Replace", SString, [("text", SString), ("search", SString), ("replacement", SString)]),
            StaticMethod("Trim", SString, [("text", SString)]),
            StaticMethod("Trim", SString, [("text", false, SString), ("trimCharacters", true, CharBuffer)]),
            StaticMethod("TrimStart", SString, [("text", SString)]),
            StaticMethod("TrimStart", SString, [("text", false, SString), ("trimCharacters", true, CharBuffer)]),
            StaticMethod("TrimEnd", SString, [("text", SString)]),
            StaticMethod("TrimEnd", SString, [("text", false, SString), ("trimCharacters", true, CharBuffer)]),
            StaticMethod("Contains", SBool, [("text", SString), ("substring", SString)]),
        ]);
    }

    private SynthesizedFinishedNamedTypeSymbol GenerateInt() {
        return StaticClass(_compilation, "Int", [
            StaticMethod("Parse", SInt, true, [("text", SString, true)]),
            StaticMethod("ToString", SString, true, [("num", SInt), ("format", SString)]),
        ]);
    }

    private SynthesizedFinishedNamedTypeSymbol GenerateDecimal() {
        return StaticClass(_compilation, "Decimal", [
            StaticMethod("IsNaN", SBool, [("num", SFloat64)]),
            StaticMethod("IsNaN", SBool, [("num", SFloat32)]),
            StaticMethod("IsPosInfinity", SBool, [("num", SFloat64)]),
            StaticMethod("IsPosInfinity", SBool, [("num", SFloat32)]),
            StaticMethod("IsNegInfinity", SBool, [("num", SFloat64)]),
            StaticMethod("IsNegInfinity", SBool, [("num", SFloat32)]),
            StaticMethod("IsInfinity", SBool, [("num", SFloat64)]),
            StaticMethod("IsInfinity", SBool, [("num", SFloat32)]),
            StaticMethod("Parse", SDecimal, true, [("text", SString, true)]),
            StaticMethod("ToString", SString, true, [("num", SDecimal), ("format", SString)]),
        ]);
    }

    private SynthesizedFinishedNamedTypeSymbol GenerateFloat64() {
        return StaticClass(_compilation, "Float64", [
            ConstExprField("MinValue", SFloat64, double.MinValue),
            ConstExprField("MaxValue", SFloat64, double.MaxValue),
            ConstExprField("Epsilon", SFloat64, double.Epsilon),
            ConstExprField("PositiveInfinity", SFloat64, double.PositiveInfinity),
            ConstExprField("NegativeInfinity", SFloat64, double.NegativeInfinity),
            ConstExprField("NaN", SFloat64, double.NaN),
        ]);
    }

    private SynthesizedFinishedNamedTypeSymbol GenerateFloat32() {
        return StaticClass(_compilation, "Float32", [
            ConstExprField("MinValue", SFloat32, float.MinValue),
            ConstExprField("MaxValue", SFloat32, float.MaxValue),
            ConstExprField("Epsilon", SFloat32, float.Epsilon),
            ConstExprField("PositiveInfinity", SFloat32, float.PositiveInfinity),
            ConstExprField("NegativeInfinity", SFloat32, float.NegativeInfinity),
            ConstExprField("NaN", SFloat32, float.NaN),
        ]);
    }

    private SynthesizedFinishedNamedTypeSymbol GenerateInt64() {
        return StaticClass(_compilation, "Int64", [
            ConstExprField("MinValue", SInt64, long.MinValue),
            ConstExprField("MaxValue", SInt64, long.MaxValue),
        ]);
    }

    private SynthesizedFinishedNamedTypeSymbol GenerateInt32() {
        return StaticClass(_compilation, "Int32", [
            ConstExprField("MinValue", SInt32, int.MinValue),
            ConstExprField("MaxValue", SInt32, int.MaxValue),
        ]);
    }

    private SynthesizedFinishedNamedTypeSymbol GenerateInt16() {
        return StaticClass(_compilation, "Int16", [
            ConstExprField("MinValue", SInt16, short.MinValue),
            ConstExprField("MaxValue", SInt16, short.MaxValue),
        ]);
    }

    private SynthesizedFinishedNamedTypeSymbol GenerateInt8() {
        return StaticClass(_compilation, "Int8", [
            ConstExprField("MinValue", SInt8, sbyte.MinValue),
            ConstExprField("MaxValue", SInt8, sbyte.MaxValue),
        ]);
    }

    private SynthesizedFinishedNamedTypeSymbol GenerateUInt64() {
        return StaticClass(_compilation, "UInt64", [
            ConstExprField("MinValue", SUInt64, ulong.MinValue),
            ConstExprField("MaxValue", SUInt64, ulong.MaxValue),
        ]);
    }

    private SynthesizedFinishedNamedTypeSymbol GenerateUInt32() {
        return StaticClass(_compilation, "UInt32", [
            ConstExprField("MinValue", SUInt32, uint.MinValue),
            ConstExprField("MaxValue", SUInt32, uint.MaxValue),
        ]);
    }

    private SynthesizedFinishedNamedTypeSymbol GenerateUInt16() {
        return StaticClass(_compilation, "UInt16", [
            ConstExprField("MinValue", SUInt16, ushort.MinValue),
            ConstExprField("MaxValue", SUInt16, ushort.MaxValue),
        ]);
    }

    private SynthesizedFinishedNamedTypeSymbol GenerateUInt8() {
        return StaticClass(_compilation, "UInt8", [
            ConstExprField("MinValue", SUInt8, byte.MinValue),
            ConstExprField("MaxValue", SUInt8, byte.MaxValue),
        ]);
    }

    private SynthesizedFinishedNamedTypeSymbol GenerateCallingConvention() {
        return StaticClass(_compilation, "CallingConvention", [
            ConstExprField("Winapi", SUInt32, (uint)1),
            ConstExprField("Cdecl", SUInt32, (uint)2),
        ]);
    }

    private SynthesizedFinishedNamedTypeSymbol GenerateTime() {
        return StaticClass(_compilation, "Time", [
            StaticMethod("Now", SInt),
            StaticMethod("Sleep", SVoid, [("milliseconds", SInt)]),
        ]);
    }

    private SynthesizedFinishedNamedTypeSymbol GenerateLowLevel() {
        var lengthT = new SynthesizedTemplateParameterSymbol(
            null,
            new TypeWithAnnotations(_compilation.GetSpecialType(SpecialType.Type)),
            0,
            "T"
        );

        var length = new SynthesizedTemplateMethodSymbol(
            "Length",
            null,
            new TypeWithAnnotations(_compilation.GetSpecialType(SpecialType.Int)),
            [lengthT],
            [SynthesizedParameterSymbol.Create(null, new TypeWithAnnotations(ArrayTypeSymbol.CreateSZArray(_compilation.assembly, new TypeWithAnnotations(lengthT))), 0, RefKind.None, "array", isConst: true)],
            MethodKind.Ordinary,
            DeclarationModifiers.Static
        );

        var sortT = new SynthesizedTemplateParameterSymbol(
            null,
            new TypeWithAnnotations(_compilation.GetSpecialType(SpecialType.Type)),
            0,
            "T"
        );

        var sort = new SynthesizedTemplateMethodSymbol(
            "Sort",
            null,
            new TypeWithAnnotations(_compilation.GetSpecialType(SpecialType.Void)),
            [sortT],
            [SynthesizedParameterSymbol.Create(null, new TypeWithAnnotations(ArrayTypeSymbol.CreateSZArray(_compilation.assembly, new TypeWithAnnotations(sortT))), 0, RefKind.None, "array")],
            MethodKind.Ordinary,
            DeclarationModifiers.Static
        );

        var fillT = new SynthesizedTemplateParameterSymbol(
            null,
            new TypeWithAnnotations(_compilation.GetSpecialType(SpecialType.Type)),
            0,
            "TElem"
        );

        var fill = new SynthesizedTemplateMethodSymbol(
            "Fill",
            null,
            new TypeWithAnnotations(_compilation.GetSpecialType(SpecialType.Void)),
            [fillT],
            [SynthesizedParameterSymbol.Create(null, new TypeWithAnnotations(ArrayTypeSymbol.CreateSZArray(_compilation.assembly, new TypeWithAnnotations(fillT))), 0, RefKind.None, "array"),
             SynthesizedParameterSymbol.Create(null, new TypeWithAnnotations(fillT), 0, RefKind.None, "value")],
            MethodKind.Ordinary,
            DeclarationModifiers.Static
        );

        var sizeOfT = new SynthesizedTemplateParameterSymbol(
            null,
            new TypeWithAnnotations(_compilation.GetSpecialType(SpecialType.Type)),
            0,
            "T"
        );

        var sizeOf = new SynthesizedTemplateMethodSymbol(
            "SizeOf",
            null,
            new TypeWithAnnotations(_compilation.GetSpecialType(SpecialType.Int32)),
            [sizeOfT],
            [],
            MethodKind.Ordinary,
            DeclarationModifiers.Static
        );

        var bitCastTFrom = new SynthesizedTemplateParameterSymbol(
            null,
            new TypeWithAnnotations(_compilation.GetSpecialType(SpecialType.Type)),
            0,
            "TFrom"
        );

        var bitCastTTo = new SynthesizedTemplateParameterSymbol(
            null,
            new TypeWithAnnotations(_compilation.GetSpecialType(SpecialType.Type)),
            0,
            "TTo"
        );

        var bitCast = new SynthesizedTemplateMethodSymbol(
            "BitCast",
            null,
            new TypeWithAnnotations(bitCastTTo),
            [bitCastTFrom, bitCastTTo],
            [SynthesizedParameterSymbol.Create(null, new TypeWithAnnotations(bitCastTFrom), 0, RefKind.None, "value")],
            MethodKind.Ordinary,
            DeclarationModifiers.Static
        );

        var createLPCSTR =
            new SynthesizedFinishedMethodSymbol(
                new SynthesizedSimpleOrdinaryMethodSymbol(
                    "CreateLPCSTR",
                    new TypeWithAnnotations(new PointerTypeSymbol(new TypeWithAnnotations(_compilation.GetSpecialType(SpecialType.UInt8)))),
                    RefKind.None,
                    DeclarationModifiers.Public | DeclarationModifiers.Static
                ),
            null,
            [SynthesizedParameterSymbol.Create(null, new TypeWithAnnotations(_compilation.GetSpecialType(SpecialType.String)), 0, RefKind.None, "str")]
        );

        var createLPCSTR_UTF =
            new SynthesizedFinishedMethodSymbol(
                new SynthesizedSimpleOrdinaryMethodSymbol(
                    "CreateLPCSTR_UTF",
                    new TypeWithAnnotations(new PointerTypeSymbol(new TypeWithAnnotations(_compilation.GetSpecialType(SpecialType.UInt8)))),
                    RefKind.None,
                    DeclarationModifiers.Public | DeclarationModifiers.Static
                ),
            null,
            [SynthesizedParameterSymbol.Create(null, new TypeWithAnnotations(_compilation.GetSpecialType(SpecialType.String)), 0, RefKind.None, "str")]
        );

        var createLPCWSTR =
            new SynthesizedFinishedMethodSymbol(
                new SynthesizedSimpleOrdinaryMethodSymbol(
                    "CreateLPCWSTR",
                    new TypeWithAnnotations(new PointerTypeSymbol(new TypeWithAnnotations(_compilation.GetSpecialType(SpecialType.Char)))),
                    RefKind.None,
                    DeclarationModifiers.Public | DeclarationModifiers.Static
                ),
            null,
            [SynthesizedParameterSymbol.Create(null, new TypeWithAnnotations(_compilation.GetSpecialType(SpecialType.String)), 0, RefKind.None, "str")]
        );

        var freeLPCSTR =
            new SynthesizedFinishedMethodSymbol(
                new SynthesizedSimpleOrdinaryMethodSymbol(
                    "FreeLPCSTR",
                    new TypeWithAnnotations(_compilation.GetSpecialType(SpecialType.Void)),
                    RefKind.None,
                    DeclarationModifiers.Public | DeclarationModifiers.Static
                ),
                null,
                [SynthesizedParameterSymbol.Create(null, new TypeWithAnnotations(new PointerTypeSymbol(new TypeWithAnnotations(_compilation.GetSpecialType(SpecialType.UInt8)))), 0, RefKind.None, "str")]
        );

        var freeLPCWSTR =
            new SynthesizedFinishedMethodSymbol(
                new SynthesizedSimpleOrdinaryMethodSymbol(
                    "FreeLPCWSTR",
                    new TypeWithAnnotations(_compilation.GetSpecialType(SpecialType.Void)),
                    RefKind.None,
                    DeclarationModifiers.Public | DeclarationModifiers.Static
                ),
                null,
                [SynthesizedParameterSymbol.Create(null, new TypeWithAnnotations(new PointerTypeSymbol(new TypeWithAnnotations(_compilation.GetSpecialType(SpecialType.Char)))), 0, RefKind.None, "str")]
        );

        var readLPCSTR =
            new SynthesizedFinishedMethodSymbol(
                new SynthesizedSimpleOrdinaryMethodSymbol(
                    "ReadLPCSTR",
                    new TypeWithAnnotations(_compilation.GetSpecialType(SpecialType.String)),
                    RefKind.None,
                    DeclarationModifiers.Public | DeclarationModifiers.Static
                ),
                null,
                [SynthesizedParameterSymbol.Create(null, new TypeWithAnnotations(new PointerTypeSymbol(new TypeWithAnnotations(_compilation.GetSpecialType(SpecialType.UInt8)))), 0, RefKind.None, "ptr")]
        );

        var readLPCWSTR =
            new SynthesizedFinishedMethodSymbol(
                new SynthesizedSimpleOrdinaryMethodSymbol(
                    "ReadLPCWSTR",
                    new TypeWithAnnotations(_compilation.GetSpecialType(SpecialType.String)),
                    RefKind.None,
                    DeclarationModifiers.Public | DeclarationModifiers.Static
                ),
                null,
                [SynthesizedParameterSymbol.Create(null, new TypeWithAnnotations(new PointerTypeSymbol(new TypeWithAnnotations(_compilation.GetSpecialType(SpecialType.Char)))), 0, RefKind.None, "ptr")]
        );

        var getGCPtr =
            new SynthesizedFinishedMethodSymbol(
                new SynthesizedSimpleOrdinaryMethodSymbol(
                    "GetGCPtr",
                    new TypeWithAnnotations(new PointerTypeSymbol(new TypeWithAnnotations(_compilation.GetSpecialType(SpecialType.Void)))),
                    RefKind.None,
                    DeclarationModifiers.Public | DeclarationModifiers.Static
                ),
                null,
                [SynthesizedParameterSymbol.Create(null, new TypeWithAnnotations(_compilation.GetSpecialType(SpecialType.Object)), 0, RefKind.None, "obj")]
        );

        var freeGCHandle =
            new SynthesizedFinishedMethodSymbol(
                new SynthesizedSimpleOrdinaryMethodSymbol(
                    "FreeGCHandle",
                    new TypeWithAnnotations(_compilation.GetSpecialType(SpecialType.Void)),
                    RefKind.None,
                    DeclarationModifiers.Public | DeclarationModifiers.Static
                ),
                null,
                [SynthesizedParameterSymbol.Create(null, new TypeWithAnnotations(new PointerTypeSymbol(new TypeWithAnnotations(_compilation.GetSpecialType(SpecialType.Void)))), 0, RefKind.None, "ptr")]
        );

        var getObject =
            new SynthesizedFinishedMethodSymbol(
                new SynthesizedSimpleOrdinaryMethodSymbol(
                    "GetObject",
                    new TypeWithAnnotations(_compilation.GetSpecialType(SpecialType.Object)),
                    RefKind.None,
                    DeclarationModifiers.Public | DeclarationModifiers.Static
                ),
                null,
                [SynthesizedParameterSymbol.Create(null, new TypeWithAnnotations(new PointerTypeSymbol(new TypeWithAnnotations(_compilation.GetSpecialType(SpecialType.Void)))), 0, RefKind.None, "ptr")]
        );

        return StaticClass(_compilation, "LowLevel", [
            StaticMethod("GetHashCode", SInt32, [("object", true, SObject)]),
            StaticMethod("CombineHashCode", SInt32, [("hash1", SInt32), ("hash2", SInt32)]),
            StaticMethod("GetTypeName", SString, [("object", true, SObject)]),
            StaticMethod("GetType", SType, [("value", true, SAny)]),
            length,
            sort,
            fill,
            sizeOf,
            bitCast,
            StaticMethod("ThrowNullConditionException", SVoid),
            createLPCSTR,
            createLPCSTR_UTF,
            createLPCWSTR,
            freeLPCWSTR,
            freeLPCSTR,
            readLPCSTR,
            readLPCWSTR,
            getGCPtr,
            freeGCHandle,
            getObject,
            StaticMethod("IsLittleEndian", SBool),
            StaticMethod("ReverseEndianness", SInt32, [("value", SInt32)]),
        ]);
    }

    private SynthesizedFinishedNamedTypeSymbol GenerateHashCode() {
        return StaticClass(_compilation, "HashCode", [
            StaticMethod("Combine", SInt32, [("hash1", SInt32), ("hash2", SInt32)]),
            StaticMethod("Combine", SInt32, [("hash1", SInt32), ("hash2", SInt32), ("hash3", SInt32)]),
            StaticMethod("Combine", SInt32, [("hash1", SInt32), ("hash2", SInt32), ("hash3", SInt32), ("hash4", SInt32)]),
            StaticMethod("Combine", SInt32, [("hash1", SInt32), ("hash2", SInt32), ("hash3", SInt32), ("hash4", SInt32), ("hash5", SInt32)]),
            StaticMethod("Combine", SInt32, [("hash1", SInt32), ("hash2", SInt32), ("hash3", SInt32), ("hash4", SInt32), ("hash5", SInt32), ("hash6", SInt32)]),
            StaticMethod("Combine", SInt32, [("hash1", SInt32), ("hash2", SInt32), ("hash3", SInt32), ("hash4", SInt32), ("hash5", SInt32), ("hash6", SInt32), ("hash7", SInt32)]),
            StaticMethod("Combine", SInt32, [("hash1", SInt32), ("hash2", SInt32), ("hash3", SInt32), ("hash4", SInt32), ("hash5", SInt32), ("hash6", SInt32), ("hash7", SInt32), ("hash8", SInt32)]),
        ]);
    }

    private SynthesizedFinishedNamedTypeSymbol GenerateDirectory() {
        return StaticClass(_compilation, "Directory", [
            StaticMethod("Create", SVoid, [("path", SString)]),
            StaticMethod("Delete", SVoid, [("path", SString)]),
            StaticMethod("Exists", SBool, [("path", SString)]),
            StaticMethod("GetCurrentDirectory", SString),
            // StaticMethod("GetDirectories", StringList, [("path", SpecialType.String)]),
            // StaticMethod("GetFiles", StringList, [("path", SpecialType.String)]),
        ]);
    }

    private SynthesizedFinishedNamedTypeSymbol GenerateFile() {
        return StaticClass(_compilation, "File", [
            // StaticMethod("AppendLines", SpecialType.Void, [("fileName", SpecialType.String), ("lines", StringList)]),
            StaticMethod("AppendText", SVoid, [("fileName", SString), ("text", SString)]),
            StaticMethod("Create", SVoid, [("path", SString)]),
            StaticMethod("Copy", SVoid, [("sourceFileName", SString), ("destinationFileName", SString)]),
            StaticMethod("Delete", SVoid, [("path", SString)]),
            StaticMethod("Exists", SBool, [("path", SString)]),
            // StaticMethod("ReadLines", StringList, [("fileName", SpecialType.String)]),
            StaticMethod("ReadText", SString, true, [("fileName", SString)]),
            // StaticMethod("WriteLines", SpecialType.Void, [("fileName", SpecialType.String), ("lines", StringList)]),
            StaticMethod("WriteText", SVoid, [("fileName", SString), ("text", SString)]),
        ]);
    }

    private SynthesizedFinishedNamedTypeSymbol GenerateConsole() {
        return StaticClass(_compilation, "Console", [
            StaticClass(_compilation, "Color", [
                ConstExprField("Black", SInt, 0L),
                ConstExprField("DarkBlue", SInt, 1L),
                ConstExprField("DarkGreen", SInt, 2L),
                ConstExprField("DarkCyan", SInt, 3L),
                ConstExprField("DarkRed", SInt, 4L),
                ConstExprField("DarkMagenta", SInt, 5L),
                ConstExprField("DarkYellow", SInt, 6L),
                ConstExprField("Gray", SInt, 7L),
                ConstExprField("DarkGray", SInt, 8L),
                ConstExprField("Blue", SInt, 9L),
                ConstExprField("Green", SInt, 10L),
                ConstExprField("Cyan", SInt, 11L),
                ConstExprField("Red", SInt, 12L),
                ConstExprField("Magenta", SInt, 13L),
                ConstExprField("Yellow", SInt, 14L),
                ConstExprField("White", SInt, 15L)
            ]),
            StaticMethod("Clear", SVoid),
            StaticMethod("GetWidth", SInt),
            StaticMethod("GetHeight", SInt),
            StaticMethod("Input", SString),
            StaticMethod("PrintLine", SVoid),
            StaticMethod("PrintLine", SVoid, [("message", true, SString, true)]),
            StaticMethod("PrintLine", SVoid, [("value", true, SAny, true)]),
            StaticMethod("PrintLine", SVoid, [("chars", true, CharBuffer, true)]),
            StaticMethod("Print", SVoid, [("message", true, SString, true)]),
            StaticMethod("Print", SVoid, [("value", true, SAny, true)]),
            StaticMethod("Print", SVoid, [("chars", true, CharBuffer, true)]),
            StaticMethod("ResetColor", SVoid),
            StaticMethod("SetForegroundColor", SVoid, [("color", SInt)]),
            StaticMethod("SetBackgroundColor", SVoid, [("color", SInt)]),
            StaticMethod("SetCursorPosition", SVoid, [("left", SInt, true), ("top", SInt, true)]),
            StaticMethod("SetCursorVisibility", SVoid, [("visible", SBool)]),
        ]);
    }

    private SynthesizedFinishedNamedTypeSymbol GenerateMath() {
        return StaticClass(_compilation, "Math", [
            ConstExprField("E", SDecimal, 2.7182818284590451),
            ConstExprField("PI", SDecimal, 3.1415926535897931),
            StaticMethod("Abs", SDecimal, true, [("value", SDecimal, true)]),
            StaticMethod("Abs", SDecimal, [("value", SDecimal)]),
            StaticMethod("Abs", SInt, true, [("value", SInt, true)]),
            StaticMethod("Abs", SInt, [("value", SInt)]),
            StaticMethod("Acos", SDecimal, true, [("d", SDecimal, true)]),
            StaticMethod("Acos", SDecimal, [("d", SDecimal)]),
            StaticMethod("Acosh", SDecimal, true, [("d", SDecimal, true)]),
            StaticMethod("Acosh", SDecimal, [("d", SDecimal)]),
            StaticMethod("Asin", SDecimal, true, [("d", SDecimal, true)]),
            StaticMethod("Asin", SDecimal, [("d", SDecimal)]),
            StaticMethod("Asinh", SDecimal, true, [("d", SDecimal, true)]),
            StaticMethod("Asinh", SDecimal, [("d", SDecimal)]),
            StaticMethod("Atan", SDecimal, true, [("d", SDecimal, true)]),
            StaticMethod("Atan", SDecimal, [("d", SDecimal)]),
            StaticMethod("Atanh", SDecimal, true, [("d", SDecimal, true)]),
            StaticMethod("Atanh", SDecimal, [("d", SDecimal)]),
            StaticMethod("Ceiling", SDecimal, true, [("d", SDecimal, true)]),
            StaticMethod("Ceiling", SDecimal, [("d", SDecimal)]),
            StaticMethod("Clamp", SDecimal, true, [("value", SDecimal, true), ("min", SDecimal, true), ("max", SDecimal, true)]),
            StaticMethod("Clamp", SDecimal, [("value", SDecimal), ("min", SDecimal), ("max", SDecimal)]),
            StaticMethod("Clamp", SFloat32, true, [("value", SFloat32, true), ("min", SFloat32, true), ("max", SFloat32, true)]),
            StaticMethod("Clamp", SFloat32, [("value", SFloat32), ("min", SFloat32), ("max", SFloat32)]),
            StaticMethod("Clamp", SInt, true, [("value", SInt, true), ("min", SInt, true), ("max", SInt, true)]),
            StaticMethod("Clamp", SInt, [("value", SInt), ("min", SInt), ("max", SInt)]),
            StaticMethod("Clamp", SUInt64, true, [("value", SUInt64, true), ("min", SUInt64, true), ("max", SUInt64, true)]),
            StaticMethod("Clamp", SUInt64, [("value", SUInt64), ("min", SUInt64), ("max", SUInt64)]),
            StaticMethod("Clamp", SInt32, true, [("value", SInt32, true), ("min", SInt32, true), ("max", SInt32, true)]),
            StaticMethod("Clamp", SInt32, [("value", SInt32), ("min", SInt32), ("max", SInt32)]),
            StaticMethod("Clamp", SUInt32, true, [("value", SUInt32, true), ("min", SUInt32, true), ("max", SUInt32, true)]),
            StaticMethod("Clamp", SUInt32, [("value", SUInt32), ("min", SUInt32), ("max", SUInt32)]),
            StaticMethod("Clamp", SInt16, true, [("value", SInt16, true), ("min", SInt16, true), ("max", SInt16, true)]),
            StaticMethod("Clamp", SInt16, [("value", SInt16), ("min", SInt16), ("max", SInt16)]),
            StaticMethod("Clamp", SUInt16, true, [("value", SUInt16, true), ("min", SUInt16, true), ("max", SUInt16, true)]),
            StaticMethod("Clamp", SUInt16, [("value", SUInt16), ("min", SUInt16), ("max", SUInt16)]),
            StaticMethod("Clamp", SInt8, true, [("value", SInt8, true), ("min", SInt8, true), ("max", SInt8, true)]),
            StaticMethod("Clamp", SInt8, [("value", SInt8), ("min", SInt8), ("max", SInt8)]),
            StaticMethod("Clamp", SUInt8, true, [("value", SUInt8, true), ("min", SUInt8, true), ("max", SUInt8, true)]),
            StaticMethod("Clamp", SUInt8, [("value", SUInt8), ("min", SUInt8), ("max", SUInt8)]),
            StaticMethod("Clamp", SChar, true, [("value", SChar, true), ("min", SChar, true), ("max", SChar, true)]),
            StaticMethod("Clamp", SChar, [("value", SChar), ("min", SChar), ("max", SChar)]),
            StaticMethod("Cos", SDecimal, true, [("d", SDecimal, true)]),
            StaticMethod("Cos", SDecimal, [("d", SDecimal)]),
            StaticMethod("Cosh", SDecimal, true, [("d", SDecimal, true)]),
            StaticMethod("Cosh", SDecimal, [("d", SDecimal)]),
            StaticMethod("Exp", SDecimal, true, [("d", SDecimal, true)]),
            StaticMethod("Exp", SDecimal, [("d", SDecimal)]),
            StaticMethod("Floor", SDecimal, true, [("d", SDecimal, true)]),
            StaticMethod("Floor", SDecimal, [("d", SDecimal)]),
            StaticMethod("Lerp", SDecimal, true, [("start", SDecimal, true), ("end", SDecimal, true), ("rate", SDecimal, true)]),
            StaticMethod("Lerp", SDecimal, [("start", SDecimal), ("end", SDecimal), ("rate", SDecimal)]),
            StaticMethod("Log", SDecimal, true, [("d", SDecimal, true), ("base", SDecimal, true)]),
            StaticMethod("Log", SDecimal, [("d", SDecimal), ("base", SDecimal)]),
            StaticMethod("Log", SDecimal, true, [("d", SDecimal, true)]),
            StaticMethod("Log", SDecimal, [("d", SDecimal)]),
            StaticMethod("Max", SDecimal, true, [("val1", SDecimal, true), ("val2", SDecimal, true)]),
            StaticMethod("Max", SDecimal, [("val1", SDecimal), ("val2", SDecimal)]),
            StaticMethod("Max", SFloat32, true, [("val1", SFloat32, true), ("val2", SFloat32, true)]),
            StaticMethod("Max", SFloat32, [("val1", SFloat32), ("val2", SFloat32)]),
            StaticMethod("Max", SInt, true, [("val1", SInt, true), ("val2", SInt, true)]),
            StaticMethod("Max", SInt, [("val1", SInt), ("val2", SInt)]),
            StaticMethod("Max", SUInt64, true, [("val1", SUInt64, true), ("val2", SUInt64, true)]),
            StaticMethod("Max", SUInt64, [("val1", SUInt64), ("val2", SUInt64)]),
            StaticMethod("Max", SInt32, true, [("val1", SInt32, true), ("val2", SInt32, true)]),
            StaticMethod("Max", SInt32, [("val1", SInt32), ("val2", SInt32)]),
            StaticMethod("Max", SUInt32, true, [("val1", SUInt32, true), ("val2", SUInt32, true)]),
            StaticMethod("Max", SUInt32, [("val1", SUInt32), ("val2", SUInt32)]),
            StaticMethod("Min", SDecimal, true, [("val1", SDecimal, true), ("val2", SDecimal, true)]),
            StaticMethod("Min", SDecimal, [("val1", SDecimal), ("val2", SDecimal)]),
            StaticMethod("Min", SFloat32, true, [("val1", SFloat32, true), ("val2", SFloat32, true)]),
            StaticMethod("Min", SFloat32, [("val1", SFloat32), ("val2", SFloat32)]),
            StaticMethod("Min", SInt, true, [("val1", SInt, true), ("val2", SInt, true)]),
            StaticMethod("Min", SInt, [("val1", SInt), ("val2", SInt)]),
            StaticMethod("Min", SUInt64, true, [("val1", SUInt64, true), ("val2", SUInt64, true)]),
            StaticMethod("Min", SUInt64, [("val1", SUInt64), ("val2", SUInt64)]),
            StaticMethod("Min", SInt32, true, [("val1", SInt32, true), ("val2", SInt32, true)]),
            StaticMethod("Min", SInt32, [("val1", SInt32), ("val2", SInt32)]),
            StaticMethod("Min", SUInt32, true, [("val1", SUInt32, true), ("val2", SUInt32, true)]),
            StaticMethod("Min", SUInt32, [("val1", SUInt32), ("val2", SUInt32)]),
            StaticMethod("Pow", SDecimal, true, [("x", SDecimal, true), ("y", SDecimal, true)]),
            StaticMethod("Pow", SDecimal, [("x", SDecimal), ("y", SDecimal)]),
            StaticMethod("Pow", SInt, true, [("x", SInt, true), ("y", SInt, true)]),
            StaticMethod("Pow", SInt, [("x", SInt), ("y", SInt)]),
            StaticMethod("Round", SDecimal, true, [("value", SDecimal, true)]),
            StaticMethod("Round", SDecimal, [("value", SDecimal)]),
            StaticMethod("Sign", SInt, [("value", SDecimal)]),
            StaticMethod("Sign", SInt, true, [("value", SDecimal, true)]),
            StaticMethod("Sign", SInt, [("value", SInt)]),
            StaticMethod("Sign", SInt, true, [("value", SInt, true)]),
            StaticMethod("Sin", SDecimal, true, [("d", SDecimal, true)]),
            StaticMethod("Sin", SDecimal, [("d", SDecimal)]),
            StaticMethod("Sinh", SDecimal, true, [("d", SDecimal, true)]),
            StaticMethod("Sinh", SDecimal, [("d", SDecimal)]),
            StaticMethod("Sqrt", SDecimal, true, [("d", SDecimal, true)]),
            StaticMethod("Sqrt", SDecimal, [("d", SDecimal)]),
            StaticMethod("Tan", SDecimal, true, [("d", SDecimal, true)]),
            StaticMethod("Tan", SDecimal, [("d", SDecimal)]),
            StaticMethod("Tanh", SDecimal, true, [("d", SDecimal, true)]),
            StaticMethod("Tanh", SDecimal, [("d", SDecimal)]),
            StaticMethod("Truncate", SDecimal, true, [("value", SDecimal, true)]),
            StaticMethod("Truncate", SDecimal, [("value", SDecimal)]),
            StaticMethod("DegToRad", SDecimal, true, [("degrees", SDecimal, true)]),
            StaticMethod("DegToRad", SDecimal, [("degrees", SDecimal)]),
            StaticMethod("RadToDeg", SDecimal, true, [("radians", SDecimal, true)]),
            StaticMethod("RadToDeg", SDecimal, [("radians", SDecimal)]),
        ]);
    }

    private Dictionary<string, Func<object, object, object, object>> GenerateEvaluatorMap() {
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
            { "Console_PrintLine_[?", new Func<object, object, object, object>((a, b, c)
                => { if (!System.Console.IsOutputRedirected) System.Console.WriteLine(Array.ConvertAll((object[])a, i => (char)i)); return null; }) },
            { "Console_Print_S?", new Func<object, object, object, object>((a, b, c)
                => { if (!System.Console.IsOutputRedirected) System.Console.Write(a); return null; }) },
            { "Console_Print_A?", new Func<object, object, object, object>((a, b, c)
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
            { "Math_Clamp_F4?F4?F4?", new Func<object, object, object, object>((a, b, c)
                => { return (a is null || b is null || c is null) ? null : System.Math.Clamp(Convert.ToSingle(a), Convert.ToSingle(b), Convert.ToSingle(c)); }) },
            { "Math_Clamp_F4F4F4", new Func<object, object, object, object>((a, b, c)
                => { return System.Math.Clamp(Convert.ToSingle(a), Convert.ToSingle(b), Convert.ToSingle(c)); }) },
            { "Math_Clamp_I?I?I?", new Func<object, object, object, object>((a, b, c)
                => { return (a is null || b is null || c is null) ? null : System.Math.Clamp((long)a, (long)b, (long)c); }) },
            { "Math_Clamp_III", new Func<object, object, object, object>((a, b, c)
                => { return System.Math.Clamp((long)a, (long)b, (long)c); }) },
            { "Math_Clamp_U8?U8?U8?", new Func<object, object, object, object>((a, b, c)
                => { return (a is null || b is null || c is null) ? null : System.Math.Clamp((ulong)a, (ulong)b, (ulong)c); }) },
            { "Math_Clamp_U8U8U8", new Func<object, object, object, object>((a, b, c)
                => { return System.Math.Clamp((ulong)a, (ulong)b, (ulong)c); }) },
            { "Math_Clamp_I4?I4?I4?", new Func<object, object, object, object>((a, b, c)
                => { return (a is null || b is null || c is null) ? null : System.Math.Clamp((int)a, (int)b, (int)c); }) },
            { "Math_Clamp_I4I4I4", new Func<object, object, object, object>((a, b, c)
                => { return System.Math.Clamp((int)a, (int)b, (int)c); }) },
            { "Math_Clamp_U4?U4?U4?", new Func<object, object, object, object>((a, b, c)
                => { return (a is null || b is null || c is null) ? null : System.Math.Clamp((uint)a, (uint)b, (uint)c); }) },
            { "Math_Clamp_U4U4U4", new Func<object, object, object, object>((a, b, c)
                => { return System.Math.Clamp((uint)a, (uint)b, (uint)c); }) },
            { "Math_Clamp_I2?I2?I2?", new Func<object, object, object, object>((a, b, c)
                => { return (a is null || b is null || c is null) ? null : System.Math.Clamp((short)a, (short)b, (short)c); }) },
            { "Math_Clamp_I2I2I2", new Func<object, object, object, object>((a, b, c)
                => { return System.Math.Clamp((short)a, (short)b, (short)c); }) },
            { "Math_Clamp_U2?U2?U2?", new Func<object, object, object, object>((a, b, c)
                => { return (a is null || b is null || c is null) ? null : System.Math.Clamp((ushort)a, (ushort)b, (ushort)c); }) },
            { "Math_Clamp_U2U2U2", new Func<object, object, object, object>((a, b, c)
                => { return System.Math.Clamp((ushort)a, (ushort)b, (ushort)c); }) },
            { "Math_Clamp_I1?I1?I1?", new Func<object, object, object, object>((a, b, c)
                => { return (a is null || b is null || c is null) ? null : System.Math.Clamp((sbyte)a, (sbyte)b, (sbyte)c); }) },
            { "Math_Clamp_I1I1I1", new Func<object, object, object, object>((a, b, c)
                => { return System.Math.Clamp((sbyte)a, (sbyte)b, (sbyte)c); }) },
            { "Math_Clamp_U1?U1?U1?", new Func<object, object, object, object>((a, b, c)
                => { return (a is null || b is null || c is null) ? null : System.Math.Clamp((byte)a, (byte)b, (byte)c); }) },
            { "Math_Clamp_U1U1U1", new Func<object, object, object, object>((a, b, c)
                => { return System.Math.Clamp((byte)a, (byte)b, (byte)c); }) },
            { "Math_Clamp_C?C?C?", new Func<object, object, object, object>((a, b, c)
                => { return (a is null || b is null || c is null) ? null : System.Math.Clamp((char)a, (char)b, (char)c); }) },
            { "Math_Clamp_CCC", new Func<object, object, object, object>((a, b, c)
                => { return System.Math.Clamp((char)a, (char)b, (char)c); }) },
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
            { "Math_Max_F4?F4?", new Func<object, object, object, object>((a, b, c)
                => { return a is null || b is null ? null : System.Math.Max(Convert.ToSingle(a), Convert.ToSingle(b)); }) },
            { "Math_Max_F4F4", new Func<object, object, object, object>((a, b, c)
                => { return System.Math.Max(Convert.ToSingle(a), Convert.ToSingle(b)); }) },
            { "Math_Max_I?I?", new Func<object, object, object, object>((a, b, c)
                => { return a is null || b is null ? null : System.Math.Max((long)a, (long)b); }) },
            { "Math_Max_II", new Func<object, object, object, object>((a, b, c)
                => { return System.Math.Max((long)a, (long)b); }) },
            { "Math_Max_I4?I4?", new Func<object, object, object, object>((a, b, c)
                => { return a is null || b is null ? null : System.Math.Max((int)a, (int)b); }) },
            { "Math_Max_I4I4", new Func<object, object, object, object>((a, b, c)
                => { return System.Math.Max((int)a, (int)b); }) },
            { "Math_Max_U8?U8?", new Func<object, object, object, object>((a, b, c)
                => { return a is null || b is null ? null : System.Math.Max((ulong)a, (ulong)b); }) },
            { "Math_Max_U8U8", new Func<object, object, object, object>((a, b, c)
                => { return System.Math.Max((ulong)a, (ulong)b); }) },
            { "Math_Max_U4?U4?", new Func<object, object, object, object>((a, b, c)
                => { return a is null || b is null ? null : System.Math.Max((uint)a, (uint)b); }) },
            { "Math_Max_U4U4", new Func<object, object, object, object>((a, b, c)
                => { return System.Math.Max((uint)a, (uint)b); }) },
            { "Math_Min_D?D?", new Func<object, object, object, object>((a, b, c)
                => { return a is null || b is null ? null : System.Math.Min(Convert.ToDouble(a), Convert.ToDouble(b)); }) },
            { "Math_Min_DD", new Func<object, object, object, object>((a, b, c)
                => { return System.Math.Min(Convert.ToDouble(a), Convert.ToDouble(b)); }) },
            { "Math_Min_F4?F4?", new Func<object, object, object, object>((a, b, c)
                => { return a is null || b is null ? null : System.Math.Min(Convert.ToSingle(a), Convert.ToSingle(b)); }) },
            { "Math_Min_F4F4", new Func<object, object, object, object>((a, b, c)
                => { return System.Math.Min(Convert.ToSingle(a), Convert.ToSingle(b)); }) },
            { "Math_Min_I?I?", new Func<object, object, object, object>((a, b, c)
                => { return a is null || b is null ? null : System.Math.Min((long)a, (long)b); }) },
            { "Math_Min_II", new Func<object, object, object, object>((a, b, c)
                => { return System.Math.Min((long)a, (long)b); }) },
            { "Math_Min_I4?I4?", new Func<object, object, object, object>((a, b, c)
                => { return a is null || b is null ? null : System.Math.Min((int)a, (int)b); }) },
            { "Math_Min_I4I4", new Func<object, object, object, object>((a, b, c)
                => { return System.Math.Min((int)a, (int)b); }) },
            { "Math_Min_U8?U8?", new Func<object, object, object, object>((a, b, c)
                => { return a is null || b is null ? null : System.Math.Min((ulong)a, (ulong)b); }) },
            { "Math_Min_U8U8", new Func<object, object, object, object>((a, b, c)
                => { return System.Math.Min((ulong)a, (ulong)b); }) },
            { "Math_Min_U4?U4?", new Func<object, object, object, object>((a, b, c)
                => { return a is null || b is null ? null : System.Math.Min((uint)a, (uint)b); }) },
            { "Math_Min_U4U4", new Func<object, object, object, object>((a, b, c)
                => { return System.Math.Min((uint)a, (uint)b); }) },
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
            { "String_IndexOf_SC", new Func<object, object, object, object>((a, b, c)
                => { return (long)((string)a).IndexOf((char)b); }) },
            { "String_Substring_SI?I?", new Func<object, object, object, object>((a, b, c)
                => { if (a is null) return null;
                     if (c is null) return ((string)a).Substring(b is null ? 0 : unchecked((int)(long)b));
                     return ((string)a).Substring(b is null ? 0 : unchecked((int)(long)b), unchecked((int)(long)c)); }) },
            { "String_PadLeft_SCI", new Func<object, object, object, object>((a, b, c)
                => { return ((string)a).PadLeft((int)(long)c, (char)b); }) },
            { "String_PadRight_SCI", new Func<object, object, object, object>((a, b, c)
                => { return ((string)a).PadRight((int)(long)c, (char)b); }) },
            { "String_Replace_SSS", new Func<object, object, object, object>((a, b, c)
                => { return ((string)a).Replace((string)b, (string)c); }) },
            { "String_Trim_S", new Func<object, object, object, object>((a, b, c)
                => { return ((string)a).Trim(); }) },
            { "String_Trim_S[", new Func<object, object, object, object>((a, b, c)
                => { return ((string)a).Trim(Array.ConvertAll((object[])b, i => (char)i)); }) },
            { "String_TrimStart_S", new Func<object, object, object, object>((a, b, c)
                => { return ((string)a).TrimStart(); }) },
            { "String_TrimStart_S[", new Func<object, object, object, object>((a, b, c)
                => { return ((string)a).TrimStart(Array.ConvertAll((object[])b, i => (char)i)); }) },
            { "String_TrimEnd_S", new Func<object, object, object, object>((a, b, c)
                => { return ((string)a).TrimEnd(); }) },
            { "String_TrimEnd_S[", new Func<object, object, object, object>((a, b, c)
                => { return ((string)a).TrimEnd(Array.ConvertAll((object[])b, i => (char)i)); }) },
            { "String_Contains_SS", new Func<object, object, object, object>((a, b, c)
                => { return ((string)a).Contains((string)b); }) },
            { "Int_Parse_S?", new Func<object, object, object, object>((a, b, c)
                => { if (a is null) return null;
                     if (long.TryParse((string)a, out var result)) return result;
                     return null; }) },
            { "Int_ToString_IS", new Func<object, object, object, object>((a, b, c)
                => { return ((long)a).ToString((string)b); }) },
            { "Decimal_IsNaN_F4", new Func<object, object, object, object>((a, b, c)
                => { return float.IsNaN((float)a); }) },
            { "Decimal_IsPosInfinity_F4", new Func<object, object, object, object>((a, b, c)
                => { return float.IsPositiveInfinity((float)a); }) },
            { "Decimal_IsNegInfinity_F4", new Func<object, object, object, object>((a, b, c)
                => { return float.IsNegativeInfinity((float)a); }) },
            { "Decimal_IsInfinity_F4", new Func<object, object, object, object>((a, b, c)
                => { return float.IsInfinity((float)a); }) },
            { "Decimal_IsNaN_F8", new Func<object, object, object, object>((a, b, c)
                => { return double.IsNaN((double)a); }) },
            { "Decimal_IsPosInfinity_F8", new Func<object, object, object, object>((a, b, c)
                => { return double.IsPositiveInfinity((double)a); }) },
            { "Decimal_IsNegInfinity_F8", new Func<object, object, object, object>((a, b, c)
                => { return double.IsNegativeInfinity((double)a); }) },
            { "Decimal_IsInfinity_F8", new Func<object, object, object, object>((a, b, c)
                => { return double.IsInfinity((double)a); }) },
            { "Decimal_Parse_S?", new Func<object, object, object, object>((a, b, c)
                => { if (a is null) return null;
                     if (double.TryParse((string)a, out var result)) return result;
                     return null; }) },
            { "Decimal_ToString_DS", new Func<object, object, object, object>((a, b, c)
                => { return ((double)a).ToString((string)b); }) },
        };
    }
}
