using System;
using System.Collections.Immutable;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Symbols;

internal readonly struct TypedConstant : IEquatable<TypedConstant> {
    private readonly object _value;

    internal TypedConstant(TypeSymbol type, TypedConstantKind kind, object value) {
        this.kind = kind;
        this.type = type;
        _value = value;
    }

    internal TypedConstant(TypeSymbol type, ImmutableArray<TypedConstant> array)
        : this(type, TypedConstantKind.Array, value: array.IsDefault ? null : (object)array) { }

    internal TypedConstantKind kind { get; }

    internal TypeSymbol type { get; }

    internal bool isNull => _value is null;

    internal object value {
        get {
            var result = valueInternal;

            if (result is Symbol symbol)
                return symbol;

            return result;
        }
    }

    internal object valueInternal {
        get {
            if (kind == TypedConstantKind.Array)
                throw new InvalidOperationException("TypedConstant is an array. Use Values property.");

            return _value;
        }
    }

    internal ImmutableArray<TypedConstant> values {
        get {
            if (kind != TypedConstantKind.Array)
                throw new InvalidOperationException("TypedConstant is not an array. Use Value property.");

            if (isNull)
                return default;

            return (ImmutableArray<TypedConstant>)_value;
        }
    }

    internal T DecodeValue<T>(SpecialType specialType) {
        TryDecodeValue(specialType, out T value);
        return value;
    }

    internal bool TryDecodeValue<T>(SpecialType specialType, out T value) {
        if (kind == TypedConstantKind.Error) {
            value = default;
            return false;
        }

        if (type.specialType == specialType || (type.typeKind == TypeKind.Enum && specialType == SpecialType.Enum)) {
            value = (T)_value;
            return true;
        }

        value = default;
        return false;
    }

    public override bool Equals(object? obj) {
        return obj is TypedConstant constant && Equals(constant);
    }

    public bool Equals(TypedConstant other) {
        return kind == other.kind
            && Equals(_value, other._value)
            && Equals(type, other.type);
    }

    public override int GetHashCode() {
        return Hash.Combine(_value, Hash.Combine(type, (int)kind));
    }

    internal static TypedConstantKind GetTypedConstantKind(TypeSymbol type, Compilation compilation) {
        switch (type.specialType) {
            case SpecialType.Bool:
            case SpecialType.Int8:
            case SpecialType.Int16:
            case SpecialType.Int32:
            case SpecialType.Int64:
            case SpecialType.UInt8:
            case SpecialType.UInt16:
            case SpecialType.UInt32:
            case SpecialType.UInt64:
            case SpecialType.Float32:
            case SpecialType.Float64:
            case SpecialType.Decimal:
            case SpecialType.Int:
            case SpecialType.Char:
            case SpecialType.String:
            case SpecialType.Object:
                return TypedConstantKind.Primitive;
            default:
                switch (type.typeKind) {
                    case TypeKind.Array:
                        return TypedConstantKind.Array;
                    case TypeKind.Error:
                        return TypedConstantKind.Error;
                }

                if (compilation is not null /*&& compilation.IsSystemTypeReference(type)*/) {
                    return TypedConstantKind.Type;
                }

                return TypedConstantKind.Error;
        }
    }
}
