
namespace Buckle.CodeAnalysis.Symbols;

internal sealed partial class SynthesizedExplicitImplementationForwardingMethod : SynthesizedImplementationMethod {
    internal SynthesizedExplicitImplementationForwardingMethod(
        MethodSymbol interfaceMethod,
        MethodSymbol implementingMethod,
        NamedTypeSymbol implementingType)
        : base(interfaceMethod, implementingType) {
        this.implementingMethod = implementingMethod;
    }

    internal MethodSymbol implementingMethod { get; }

    public override MethodKind methodKind => MethodKind.ExplicitInterfaceImplementation;

    internal override bool isStatic => implementingMethod.isStatic;

    internal override bool hasSpecialName => false;
}
