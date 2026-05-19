using System;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis;

internal readonly struct BelteTypeInfo : IEquatable<BelteTypeInfo> {
    internal static readonly BelteTypeInfo None =
        new BelteTypeInfo(type: null, convertedType: null, Conversion.Identity);

    public readonly TypeSymbol type;

    public readonly TypeSymbol convertedType;

    public readonly Conversion implicitConversion;

    internal BelteTypeInfo(TypeSymbol type, TypeSymbol convertedType, Conversion implicitConversion) {
        this.type = type.GetNonErrorGuess() ?? type;
        this.convertedType = convertedType.GetNonErrorGuess() ?? convertedType;
        this.implicitConversion = implicitConversion;
    }

    public static implicit operator TypeInfo(BelteTypeInfo info) {
        return new TypeInfo(info.type, info.convertedType);
    }

    public override bool Equals(object obj) {
        return obj is BelteTypeInfo info && Equals(info);
    }

    public bool Equals(BelteTypeInfo other) {
        return implicitConversion.Equals(other.implicitConversion)
            && TypeSymbol.Equals(type, other.type, TypeCompareKind.ConsiderEverything)
            && TypeSymbol.Equals(convertedType, other.convertedType, TypeCompareKind.ConsiderEverything);
    }

    public override int GetHashCode() {
        return Hash.Combine(convertedType,
               Hash.Combine(type, implicitConversion.GetHashCode()));
    }
}
