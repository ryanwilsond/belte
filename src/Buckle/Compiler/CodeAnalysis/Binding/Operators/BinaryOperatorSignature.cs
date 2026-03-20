using System;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Binding;

internal struct BinaryOperatorSignature : IEquatable<BinaryOperatorSignature> {
    internal static BinaryOperatorSignature Error = default;

    internal readonly TypeSymbol leftType;
    internal readonly TypeSymbol rightType;
    internal readonly TypeSymbol returnType;
    internal readonly MethodSymbol method;
    internal readonly TypeSymbol constrainedToTypeOpt;
    internal readonly BinaryOperatorKind kind;

    internal BinaryOperatorSignature(
        BinaryOperatorKind kind,
        TypeSymbol leftType,
        TypeSymbol rightType,
        TypeSymbol returnType) {
        this.kind = kind;
        this.leftType = leftType;
        this.rightType = rightType;
        this.returnType = returnType;
        method = null;
        constrainedToTypeOpt = null;
    }

    internal BinaryOperatorSignature(
        BinaryOperatorKind kind,
        TypeSymbol leftType,
        TypeSymbol rightType,
        TypeSymbol returnType,
        MethodSymbol method,
        TypeSymbol constrainedToTypeOpt) {
        this.kind = kind;
        this.leftType = leftType;
        this.rightType = rightType;
        this.returnType = returnType;
        this.method = method;
        this.constrainedToTypeOpt = constrainedToTypeOpt;
    }

    internal readonly RefKind leftRefKind {
        get {
            if (method is not null) {
                if (!method.parameterRefKinds.IsDefaultOrEmpty)
                    return method.parameterRefKinds[0];
            }

            return RefKind.None;
        }
    }

    internal readonly RefKind rightRefKind {
        get {
            if (method is not null) {
                if (!method.parameterRefKinds.IsDefaultOrEmpty)
                    return method.parameterRefKinds[1];
            }

            return RefKind.None;
        }
    }

    public bool Equals(BinaryOperatorSignature other) {
        return kind == other.kind &&
            TypeSymbol.Equals(leftType, other.leftType, TypeCompareKind.ConsiderEverything) &&
            TypeSymbol.Equals(rightType, other.rightType, TypeCompareKind.ConsiderEverything) &&
            TypeSymbol.Equals(returnType, other.returnType, TypeCompareKind.ConsiderEverything) &&
            method == other.method;
    }

    public static bool operator ==(BinaryOperatorSignature x, BinaryOperatorSignature y) {
        return x.Equals(y);
    }

    public static bool operator !=(BinaryOperatorSignature x, BinaryOperatorSignature y) {
        return !x.Equals(y);
    }

    public override bool Equals(object obj) {
        return obj is BinaryOperatorSignature signature && Equals(signature);
    }

    public override int GetHashCode() {
        return Hash.Combine(returnType,
               Hash.Combine(leftType,
               Hash.Combine(rightType,
               Hash.Combine(method, (int)kind))));
    }
}
