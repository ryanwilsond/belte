using System;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis;

public readonly struct TypeInfo : IEquatable<TypeInfo> {
    internal static readonly TypeInfo None = new TypeInfo(type: null, convertedType: null);

    internal TypeInfo(ITypeSymbol type, ITypeSymbol convertedType) {
        this.type = type;
        this.convertedType = convertedType;
    }

    public ITypeSymbol type { get; }

    public ITypeSymbol convertedType { get; }

    public bool Equals(TypeInfo other) {
        return Equals(type, other.type)
            && Equals(convertedType, other.convertedType);
    }

    public override bool Equals(object obj) {
        return obj is TypeInfo info && Equals(info);
    }

    public override int GetHashCode() {
        return Hash.Combine(convertedType, type.GetHashCode());
    }
}
