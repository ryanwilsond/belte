using System.Collections.Immutable;
using System.Threading;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class SubstitutedPropertySymbol : WrappedPropertySymbol {
    private readonly SubstitutedNamedTypeSymbol _containingType;

    private TypeWithAnnotations _lazyType;
    private ImmutableArray<ParameterSymbol> _lazyParameters;

    internal SubstitutedPropertySymbol(SubstitutedNamedTypeSymbol containingType, PropertySymbol originalDefinition)
        : base(originalDefinition) {
        _containingType = containingType;
    }

    public override PropertySymbol originalDefinition => _underlyingProperty;

    internal override TypeWithAnnotations typeWithAnnotations {
        get {
            if (_lazyType is null) {
                var type = _containingType.templateSubstitution.SubstituteType(originalDefinition.typeWithAnnotations);
                Interlocked.CompareExchange(ref _lazyType, type.type, null);
            }

            return _lazyType;
        }
    }

    internal override Symbol containingSymbol => _containingType;

    internal override NamedTypeSymbol containingType => _containingType;

    internal override ImmutableArray<AttributeData> GetAttributes() {
        return originalDefinition.GetAttributes();
    }

    internal override ImmutableArray<ParameterSymbol> parameters {
        get {
            if (_lazyParameters.IsDefault)
                ImmutableInterlocked.InterlockedCompareExchange(ref _lazyParameters, SubstituteParameters(), default);

            return _lazyParameters;
        }
    }

    internal override MethodSymbol getMethod {
        get {
            var originalGetMethod = originalDefinition.getMethod;
            return originalGetMethod?.AsMember(_containingType);
        }
    }

    internal override MethodSymbol setMethod {
        get {
            var originalSetMethod = originalDefinition.setMethod;
            return originalSetMethod?.AsMember(_containingType);
        }
    }

    internal override bool isExplicitInterfaceImplementation => originalDefinition.isExplicitInterfaceImplementation;

    private ImmutableArray<PropertySymbol> _lazyExplicitInterfaceImplementations;

    private OverriddenOrHiddenMembersResult _lazyOverriddenOrHiddenMembers;

    internal override ImmutableArray<PropertySymbol> explicitInterfaceImplementations {
        get {
            if (_lazyExplicitInterfaceImplementations.IsDefault) {
                ImmutableInterlocked.InterlockedCompareExchange(
                    ref _lazyExplicitInterfaceImplementations,
                    ExplicitInterfaceHelpers.SubstituteExplicitInterfaceImplementations(
                        originalDefinition.explicitInterfaceImplementations,
                        _containingType.templateSubstitution
                    ),
                    default
                );
            }

            return _lazyExplicitInterfaceImplementations;
        }
    }

    internal override bool mustCallMethodsDirectly => originalDefinition.mustCallMethodsDirectly;

    internal override OverriddenOrHiddenMembersResult overriddenOrHiddenMembers {
        get {
            if (_lazyOverriddenOrHiddenMembers is null) {
                Interlocked.CompareExchange(
                    ref _lazyOverriddenOrHiddenMembers,
                    this.MakeOverriddenOrHiddenMembers(),
                    null
                );
            }

            return _lazyOverriddenOrHiddenMembers;
        }
    }

    private ImmutableArray<ParameterSymbol> SubstituteParameters() {
        var unsubstitutedParameters = originalDefinition.parameters;

        if (unsubstitutedParameters.IsEmpty) {
            return unsubstitutedParameters;
        } else {
            var count = unsubstitutedParameters.Length;
            var substituted = new ParameterSymbol[count];

            for (var i = 0; i < count; i++) {
                substituted[i] = new SubstitutedParameterSymbol(
                    this,
                    _containingType.templateSubstitution,
                    unsubstitutedParameters[i]
                );
            }

            return substituted.AsImmutableOrNull();
        }
    }
}
