using System.Collections.Generic;
using System.Runtime.InteropServices;
using Buckle.CodeAnalysis.Binding;
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
    public long int64;

    [FieldOffset(8)]
    public double @double;

    [FieldOffset(8)]
    public bool @bool;

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

    internal static EvaluatorValue Literal(object value, SpecialType specialType) {
        return specialType switch {
            SpecialType.Int => new EvaluatorValue() { kind = ValueKind.Int64, int64 = (long)value },
            SpecialType.Decimal => new EvaluatorValue() { kind = ValueKind.Double, @double = (double)value },
            SpecialType.Bool => new EvaluatorValue() { kind = ValueKind.Bool, @bool = (bool)value },
            SpecialType.String => new EvaluatorValue() { kind = ValueKind.String, @string = (string)value },
            SpecialType.None => Null,
            _ => throw ExceptionUtilities.UnexpectedValue(specialType),
        };
    }

    public static Dictionary<ISymbol, EvaluatorValue> GetFieldsFromPtr(EvaluatorValue value, EvaluatorContext context) {
        var heapObject = context.heap[value.ptr];

        if (heapObject is null)
            return [];

        var dictionary = new Dictionary<ISymbol, EvaluatorValue>();
        var layout = context.typeLayouts[(NamedTypeSymbol)heapObject.type];

        foreach (var local in layout.LocalsInOrder())
            dictionary.Add(local.symbol, heapObject.fields[local.slot]);

        return dictionary;
    }

    public static object Format(EvaluatorValue value, EvaluatorContext context) {
        switch (value.kind) {
            case ValueKind.Null:
                return null;
            case ValueKind.Int64:
                return value.int64;
            case ValueKind.Bool:
                return value.@bool;
            case ValueKind.Double:
                return value.@double;
            case ValueKind.String:
                return value.@string;
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
        var layout = context.typeLayouts[(NamedTypeSymbol)type];

        foreach (var local in layout.LocalsInOrder()) {
            var name = local.symbol.containingType.Equals(type)
                ? local.name
                : $"{local.symbol.containingType.name}.{local.name}";

            dictionary.Add(name, Format(fields[local.slot], context));
        }

        return dictionary;
    }
}
