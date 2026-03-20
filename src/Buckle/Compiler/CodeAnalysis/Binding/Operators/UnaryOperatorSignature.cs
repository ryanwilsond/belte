using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

internal struct UnaryOperatorSignature {
    internal static UnaryOperatorSignature Error = default;

    internal readonly MethodSymbol method;
    internal readonly TypeSymbol constrainedToTypeOpt;
    internal readonly TypeSymbol operandType;
    internal readonly TypeSymbol returnType;
    internal readonly UnaryOperatorKind kind;

    internal UnaryOperatorSignature(UnaryOperatorKind kind, TypeSymbol operandType, TypeSymbol returnType) {
        this.kind = kind;
        this.operandType = operandType;
        this.returnType = returnType;
        method = null;
        constrainedToTypeOpt = null;
    }

    internal UnaryOperatorSignature(
        UnaryOperatorKind kind,
        TypeSymbol operandType,
        TypeSymbol returnType,
        MethodSymbol method,
        TypeSymbol constrainedToTypeOpt) {
        this.kind = kind;
        this.operandType = operandType;
        this.returnType = returnType;
        this.method = method;
        this.constrainedToTypeOpt = constrainedToTypeOpt;
    }

    internal readonly RefKind refKind {
        get {
            if (method is not null) {
                if (!method.parameterRefKinds.IsDefaultOrEmpty)
                    return method.parameterRefKinds[0];
            }

            return RefKind.None;
        }
    }
}
