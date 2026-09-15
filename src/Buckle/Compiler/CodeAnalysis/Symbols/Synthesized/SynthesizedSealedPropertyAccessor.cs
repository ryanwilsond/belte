using System.Collections.Immutable;
using System.Diagnostics;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed partial class SynthesizedSealedPropertyAccessor : SynthesizedMethodSymbol {
    private readonly PropertySymbol _property;
    private readonly ImmutableArray<ParameterSymbol> _parameters;

    internal SynthesizedSealedPropertyAccessor(PropertySymbol property, MethodSymbol overriddenAccessor) {
        Debug.Assert(property is not null);
        Debug.Assert(property.isSealed);
        Debug.Assert(overriddenAccessor is not null);

        _property = property;
        this.overriddenAccessor = overriddenAccessor;
        _parameters = SynthesizedParameterSymbol.DeriveParameters(overriddenAccessor, this);
    }

    internal MethodSymbol overriddenAccessor { get; }

    internal override Symbol containingSymbol => _property.containingType;

    internal override ImmutableArray<TextLocation> locations => [];

    internal override TextLocation location => null;

    internal override Accessibility declaredAccessibility {
        get {
            var overriddenAccessibility = overriddenAccessor.declaredAccessibility;

            switch (overriddenAccessibility) {
                case Accessibility.InternalOrProtected:
                    if (!containingAssembly.HasInternalAccessTo(overriddenAccessor.containingAssembly))
                        return Accessibility.Protected;

                    break;
                case Accessibility.InternalAndProtected:
                    if (!containingAssembly.HasInternalAccessTo(overriddenAccessor.containingAssembly))
                        return Accessibility.Private;

                    break;
            }

            return overriddenAccessibility;
        }
    }

    internal override bool isStatic => false;

    internal override bool isVirtual => false;

    internal override CallingConvention callingConvention => overriddenAccessor.callingConvention;

    public override MethodKind methodKind => overriddenAccessor.methodKind;

    public override int arity => 0;

    internal override bool hidesBaseMethodsByName => false;

    public override bool returnsVoid => overriddenAccessor.returnsVoid;

    public override RefKind refKind => overriddenAccessor.refKind;

    internal override TypeWithAnnotations returnTypeWithAnnotations => overriddenAccessor.returnTypeWithAnnotations;

    public override ImmutableArray<BoundExpression> templateConstraints => [];

    public override ImmutableArray<TypeOrConstant> templateArguments => [];

    public override ImmutableArray<TemplateParameterSymbol> templateParameters => [];

    internal override ImmutableArray<ParameterSymbol> parameters => _parameters;

    internal override bool isExplicitInterfaceImplementation => false;

    internal override ImmutableArray<MethodSymbol> explicitInterfaceImplementations => [];

    public override Symbol associatedSymbol => _property;

    internal override bool isOverride => true;

    internal override bool isAbstract => false;

    internal override bool isSealed => true;

    internal override bool isExtern => false;

    public override string name => overriddenAccessor.name;

    internal override bool hasSpecialName => true;

    internal override bool isMetadataFinal => true;

    internal override bool IsMetadataVirtual(bool forceComplete = false) {
        return true;
    }

    internal override DllImportData GetDllImportData() {
        return null;
    }

    internal override ImmutableArray<string> GetAppliedConditionalSymbols() {
        return [];
    }
}
