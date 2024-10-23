using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class WrappedMethodSymbol : MethodSymbol {
    internal WrappedMethodSymbol(MethodSymbol underlyingMethod) {
        this.underlyingMethod = underlyingMethod;
    }

    public override string name => underlyingMethod.name;

    internal MethodSymbol underlyingMethod { get; }

    internal override bool isTemplateMethod => underlyingMethod.isTemplateMethod;

    internal override int arity => underlyingMethod.arity;

    internal override RefKind refKind => underlyingMethod.refKind;

    internal override int parameterCount => underlyingMethod.parameterCount;

    internal override bool hidesBaseMethodsByName => underlyingMethod.hidesBaseMethodsByName;

    internal override SyntaxReference syntaxReference => underlyingMethod.syntaxReference;

    internal override Accessibility declaredAccessibility => underlyingMethod.declaredAccessibility;

    internal override bool isStatic => underlyingMethod.isStatic;

    internal override bool requiresInstanceReceiver => underlyingMethod.requiresInstanceReceiver;

    internal override bool isVirtual => underlyingMethod.isVirtual;

    internal override bool isOverride => underlyingMethod.isOverride;

    internal override bool isAbstract => underlyingMethod.isAbstract;

    internal override bool isSealed => underlyingMethod.isSealed;

    internal override bool isImplicitlyDeclared => underlyingMethod.isImplicitlyDeclared;

    internal override bool hasSpecialName => underlyingMethod.hasSpecialName;

    internal override MethodKind methodKind => underlyingMethod.methodKind;

    internal override bool returnsVoid => underlyingMethod.returnsVoid;

    internal override bool isConst => underlyingMethod.isConst;
}
