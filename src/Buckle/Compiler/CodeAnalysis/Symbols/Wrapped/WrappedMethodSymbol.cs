using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class WrappedMethodSymbol : MethodSymbol {
    internal WrappedMethodSymbol(MethodSymbol underlyingMethod) {
        this.underlyingMethod = underlyingMethod;
    }

    public override string name => underlyingMethod.name;

    public override bool isTemplateMethod => underlyingMethod.isTemplateMethod;

    public override int arity => underlyingMethod.arity;

    public override RefKind refKind => underlyingMethod.refKind;

    public override MethodKind methodKind => underlyingMethod.methodKind;

    public override bool returnsVoid => underlyingMethod.returnsVoid;

    internal MethodSymbol underlyingMethod { get; }

    internal override int parameterCount => underlyingMethod.parameterCount;

    internal override bool hidesBaseMethodsByName => underlyingMethod.hidesBaseMethodsByName;

    internal override SyntaxReference syntaxReference => underlyingMethod.syntaxReference;

    internal override TextLocation location => underlyingMethod.location;

    internal override Accessibility declaredAccessibility => underlyingMethod.declaredAccessibility;

    internal override bool isStatic => underlyingMethod.isStatic;

    internal override bool requiresInstanceReceiver => underlyingMethod.requiresInstanceReceiver;

    internal override bool isVirtual => underlyingMethod.isVirtual;

    internal override bool isOverride => underlyingMethod.isOverride;

    internal override bool isAbstract => underlyingMethod.isAbstract;

    internal override bool isSealed => underlyingMethod.isSealed;

    internal override bool isExtern => underlyingMethod.isExtern;

    internal override bool isImplicitlyDeclared => underlyingMethod.isImplicitlyDeclared;

    internal override bool hasSpecialName => underlyingMethod.hasSpecialName;

    internal override bool isDeclaredConst => underlyingMethod.isDeclaredConst;

    internal override CallingConvention callingConvention => underlyingMethod.callingConvention;

    internal sealed override bool hasUnscopedRefAttribute => underlyingMethod.hasUnscopedRefAttribute;

    internal override bool isMetadataFinal => underlyingMethod.isMetadataFinal;

    internal override bool IsMetadataVirtual(bool forceComplete = false)
        => underlyingMethod.IsMetadataVirtual(forceComplete);

    internal override DllImportData GetDllImportData() {
        return underlyingMethod.GetDllImportData();
    }
}
