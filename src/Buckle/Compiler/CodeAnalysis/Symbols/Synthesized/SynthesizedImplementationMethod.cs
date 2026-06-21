using System.Collections.Immutable;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class SynthesizedImplementationMethod : SynthesizedMethodSymbol {
    protected readonly MethodSymbol _interfaceMethod;
    private readonly NamedTypeSymbol _implementingType;

    private readonly ImmutableArray<MethodSymbol> _explicitInterfaceImplementations;
    private readonly ImmutableArray<TemplateParameterSymbol> _typeParameters;
    private readonly ImmutableArray<ParameterSymbol> _parameters;
    private readonly string _name;

    public SynthesizedImplementationMethod(
        MethodSymbol interfaceMethod,
        NamedTypeSymbol implementingType,
        string name = null) {
        _name = name ?? ExplicitInterfaceHelpers.GetMemberName(
            interfaceMethod.name,
            interfaceMethod.containingType,
            aliasQualifierOpt: null
        );

        _implementingType = implementingType;
        _explicitInterfaceImplementations = [interfaceMethod];

        var typeMap = interfaceMethod.containingType.templateSubstitution ?? TemplateMap.Empty;
        typeMap.WithAlphaRename(interfaceMethod, this, out _typeParameters);

        _interfaceMethod = interfaceMethod.ConstructIfTemplate(templateArguments);
        _parameters = SynthesizedParameterSymbol.DeriveParameters(_interfaceMethod, this);
    }

    public sealed override int arity => _interfaceMethod.arity;

    public sealed override bool returnsVoid => _interfaceMethod.returnsVoid;

    internal sealed override CallingConvention callingConvention => _interfaceMethod.callingConvention;

    public sealed override ImmutableArray<TemplateParameterSymbol> templateParameters => _typeParameters;

    public sealed override ImmutableArray<TypeOrConstant> templateArguments
        => GetTemplateParametersAsTemplateArguments();

    // TODO This might need to be something?
    public sealed override ImmutableArray<BoundExpression> templateConstraints => [];

    public sealed override RefKind refKind => _interfaceMethod.refKind;

    internal sealed override TypeWithAnnotations returnTypeWithAnnotations => _interfaceMethod.returnTypeWithAnnotations;

    internal sealed override ImmutableArray<ParameterSymbol> parameters => _parameters;

    internal sealed override Symbol containingSymbol => _implementingType;

    internal sealed override NamedTypeSymbol containingType => _implementingType;

    internal sealed override bool isExplicitInterfaceImplementation => true;

    internal sealed override ImmutableArray<MethodSymbol> explicitInterfaceImplementations
        => _explicitInterfaceImplementations;

    public override MethodKind methodKind => MethodKind.ExplicitInterfaceImplementation;

    internal sealed override Accessibility declaredAccessibility => Accessibility.Private;

    internal sealed override bool hidesBaseMethodsByName => false;

    internal sealed override ImmutableArray<TextLocation> locations => [];

    internal sealed override TextLocation location => null;

    internal override bool isStatic => false;

    internal sealed override bool isVirtual => false;

    internal sealed override bool isOverride => false;

    internal sealed override bool isAbstract => false;

    internal sealed override bool isSealed => false;

    internal sealed override bool isExtern => false;

    public sealed override string name => _name;

    internal override bool hasSpecialName => _interfaceMethod.hasSpecialName;

    internal override bool IsMetadataVirtual(bool forceComplete = false) {
        return !isStatic;
    }

    internal sealed override bool isMetadataFinal => !isStatic;

    internal sealed override DllImportData GetDllImportData() {
        return null;
    }
}
