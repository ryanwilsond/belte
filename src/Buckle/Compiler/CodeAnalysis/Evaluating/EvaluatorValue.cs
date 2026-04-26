using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Display;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Evaluating;

[StructLayout(LayoutKind.Explicit)]
public struct EvaluatorValue {
    public static readonly EvaluatorValue None = new EvaluatorValue();
    public static EvaluatorValue Null => new EvaluatorValue() { kind = ValueKind.Null };

    [FieldOffset(0)]
    public ValueKind kind;

    [FieldOffset(8)]
    public sbyte int8;

    [FieldOffset(8)]
    public byte uint8;

    [FieldOffset(8)]
    public short int16;

    [FieldOffset(8)]
    public ushort uint16;

    [FieldOffset(8)]
    public int int32;

    [FieldOffset(8)]
    public uint uint32;

    [FieldOffset(8)]
    public long int64;

    [FieldOffset(8)]
    public ulong uint64;

    [FieldOffset(8)]
    public float @single;

    [FieldOffset(8)]
    public double @double;

    [FieldOffset(8)]
    public bool @bool;

    [FieldOffset(8)]
    public char @char;

    [FieldOffset(8)]
    public int ptr;

    [FieldOffset(16)]
    public string @string;

    [FieldOffset(16)]
    public ITypeSymbol type;

    [FieldOffset(16)]
    internal BoundMethodGroup methodGroup;

    [FieldOffset(16)]
    public HeapObject @struct;

    [FieldOffset(16)]
    public EvaluatorValue[] loc;

    [FieldOffset(16)]
    internal object data;

    internal static EvaluatorValue HeapPtr(int index) {
        return new EvaluatorValue() { kind = ValueKind.HeapPtr, ptr = index };
    }

    internal static EvaluatorValue Ref(EvaluatorValue[] loc, int ptr) {
        return new EvaluatorValue() { kind = ValueKind.Ref, ptr = ptr, loc = loc };
    }

    internal static EvaluatorValue Type(ITypeSymbol type) {
        return new EvaluatorValue() { kind = ValueKind.Type, type = type };
    }

    internal static EvaluatorValue MethodGroup(BoundMethodGroup methodGroup) {
        return new EvaluatorValue() { kind = ValueKind.MethodGroup, methodGroup = methodGroup };
    }

    internal static EvaluatorValue Struct(HeapObject structValue) {
        return new EvaluatorValue() { kind = ValueKind.Struct, @struct = structValue };
    }

    internal static EvaluatorValue Literal(SpecialType specialType) {
        return new EvaluatorValue() { kind = ValueKindExtensions.FromSpecialType(specialType), int64 = 0 };
    }

    internal static EvaluatorValue Literal(object value, SpecialType specialType) {
        if (value is null)
            return Null;

        return specialType switch {
            SpecialType.Int8 => new EvaluatorValue() { kind = ValueKind.Int8, int8 = Convert.ToSByte(value) },
            SpecialType.Int16 => new EvaluatorValue() { kind = ValueKind.Int16, int16 = Convert.ToInt16(value) },
            SpecialType.Int32 => new EvaluatorValue() { kind = ValueKind.Int32, int32 = Convert.ToInt32(value) },
            SpecialType.Int64 => new EvaluatorValue() { kind = ValueKind.Int64, int64 = Convert.ToInt64(value) },
            SpecialType.UInt8 => new EvaluatorValue() { kind = ValueKind.UInt8, uint8 = Convert.ToByte(value) },
            SpecialType.UInt16 => new EvaluatorValue() { kind = ValueKind.UInt16, uint16 = Convert.ToUInt16(value) },
            SpecialType.UInt32 => new EvaluatorValue() { kind = ValueKind.UInt32, uint32 = Convert.ToUInt32(value) },
            SpecialType.Pointer => new EvaluatorValue() { kind = ValueKind.UInt32, uint32 = Convert.ToUInt32(value) },
            SpecialType.UInt64 => new EvaluatorValue() { kind = ValueKind.UInt64, uint64 = Convert.ToUInt64(value) },
            SpecialType.Float32 => new EvaluatorValue() { kind = ValueKind.Float32, @single = Convert.ToSingle(value) },
            SpecialType.Float64 => new EvaluatorValue() { kind = ValueKind.Float64, @double = Convert.ToDouble(value) },
            SpecialType.Int => new EvaluatorValue() { kind = ValueKind.Int64, int64 = Convert.ToInt64(value) },
            SpecialType.Decimal => new EvaluatorValue() { kind = ValueKind.Float64, @double = Convert.ToDouble(value) },
            SpecialType.Bool => new EvaluatorValue() { kind = ValueKind.Bool, @bool = Convert.ToBoolean(value) },
            SpecialType.String => new EvaluatorValue() { kind = ValueKind.String, @string = Convert.ToString(value) },
            SpecialType.Char => new EvaluatorValue() { kind = ValueKind.Char, @char = Convert.ToChar(value) },
            SpecialType.None => Null,
            _ => throw ExceptionUtilities.UnexpectedValue(specialType),
        };
    }

    internal static EvaluatorValue Literal(bool value) {
        return new EvaluatorValue() { kind = ValueKind.Bool, @bool = value };
    }

    internal static EvaluatorValue Literal(char value) {
        return new EvaluatorValue() { kind = ValueKind.Char, @char = value };
    }

    internal static EvaluatorValue Literal(string value) {
        return new EvaluatorValue() { kind = ValueKind.String, @string = value };
    }

    internal static EvaluatorValue Literal(double value) {
        return new EvaluatorValue() { kind = ValueKind.Float64, @double = value };
    }

    internal static EvaluatorValue Literal(long value) {
        return new EvaluatorValue() { kind = ValueKind.Int64, int64 = value };
    }

    public static Dictionary<ISymbol, EvaluatorValue> GetFieldsFromPtr(EvaluatorValue value, EvaluatorContext context) {
        var heapObject = context.heap[value.ptr];

        if (heapObject is null)
            return [];

        var dictionary = new Dictionary<ISymbol, EvaluatorValue>();

        if (!context.program.TryGetTypeLayoutIncludingParents((NamedTypeSymbol)heapObject.type, out var layout))
            throw ExceptionUtilities.UnexpectedValue(heapObject.type);

        foreach (var local in layout.LocalsInOrder())
            dictionary.Add(local.symbol, heapObject.fields[local.slot]);

        return dictionary;
    }

    public static object Format(EvaluatorValue value, EvaluatorContext context) {
        switch (value.kind) {
            case ValueKind.Null:
                return null;
            case ValueKind.Int8:
                return value.int8;
            case ValueKind.Int16:
                return value.int16;
            case ValueKind.Int32:
                return value.int32;
            case ValueKind.Int64:
                return value.int64;
            case ValueKind.UInt8:
                return value.uint8;
            case ValueKind.UInt16:
                return value.uint16;
            case ValueKind.UInt32:
                return value.uint32;
            case ValueKind.UInt64:
                return value.uint64;
            case ValueKind.Float32:
                return value.@single;
            case ValueKind.Float64:
                return value.@double;
            case ValueKind.Bool:
                return value.@bool;
            case ValueKind.Char:
                return value.@char;
            case ValueKind.String:
                return value.@string;
            case ValueKind.Type:
                return value.type;
            case ValueKind.Ref:
                return Format(value.loc[value.ptr], context);
            case ValueKind.HeapPtr: {
                    var heapObject = context.heap[value.ptr];
                    return FormatObject(heapObject.type, heapObject.fields, context);
                }
            case ValueKind.Struct: {
                    var heapObject = value.@struct;
                    return FormatObject(heapObject.type, heapObject.fields, context);
                }
            case ValueKind.MethodGroup: {
                    if (value.data is MethodSymbol) {
                        return SymbolDisplay.ToDisplayString(
                            value.data as MethodSymbol,
                            SymbolDisplayFormat.BoundDisplayFormat
                        );
                    }

                    return DisplayText.DisplayNode(value.methodGroup).ToString();
                }
            default:
                throw ExceptionUtilities.UnexpectedValue(value.kind);
        }
    }

    private static object FormatObject(TypeSymbol type, EvaluatorValue[] fields, EvaluatorContext context) {
        if (type.IsArray()) {
            var builder = new object[fields.Length];

            for (var i = 0; i < fields.Length; i++)
                builder[i] = Format(fields[i], context);

            return builder;
        }

        var dictionary = new Dictionary<object, object>();

        if (!context.program.TryGetTypeLayoutIncludingParents((NamedTypeSymbol)type, out var layout))
            throw ExceptionUtilities.UnexpectedValue(type);

        foreach (var local in layout.LocalsInOrder()) {
            var name = local.symbol.containingType.Equals(type.originalDefinition)
                ? local.name
                : $"{local.symbol.containingType.name}.{local.name}";

            dictionary.Add(name, Format(fields[local.slot], context));
        }

        return dictionary;
    }
}
