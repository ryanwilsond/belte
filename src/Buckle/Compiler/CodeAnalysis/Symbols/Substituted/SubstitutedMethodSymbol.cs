using System.Collections.Immutable;
using System.Threading;
using Buckle.CodeAnalysis.Binding;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Symbols;

internal class SubstitutedMethodSymbol : WrappedMethodSymbol {
    private readonly TemplateMap _inputMap;

    private TypeWithAnnotations _lazyReturnType;
    private ImmutableArray<ParameterSymbol> _lazyParameters;
    private TemplateMap _lazyMap;
    private ImmutableArray<TemplateParameterSymbol> _lazyTemplateParameters;

    private OverriddenOrHiddenMembersResult _lazyOverriddenOrHiddenMembers;

    private int _hashCode;

    internal SubstitutedMethodSymbol(NamedTypeSymbol containingType, MethodSymbol originalDefinition)
        : this(containingType, containingType.templateSubstitution, originalDefinition, null) { }

    private protected SubstitutedMethodSymbol(
        NamedTypeSymbol containingType,
        TemplateMap map,
        MethodSymbol originalDefinition,
        MethodSymbol constructedFrom)
        : base(originalDefinition) {
        this.containingType = containingType;
        _inputMap = map;

        if (constructedFrom is not null) {
            this.constructedFrom = constructedFrom;
            _lazyTemplateParameters = constructedFrom.templateParameters;
            _lazyMap = map;
        } else {
            this.constructedFrom = this;
        }
    }

    public sealed override ImmutableArray<TemplateParameterSymbol> templateParameters {
        get {
            EnsureMapAndTemplateParameters();
            return _lazyTemplateParameters;
        }
    }

    public sealed override TemplateMap templateSubstitution {
        get {
            EnsureMapAndTemplateParameters();
            return _lazyMap;
        }
    }

    // TODO this should be something
    public sealed override ImmutableArray<BoundExpression> templateConstraints => [];

    public override ImmutableArray<TypeOrConstant> templateArguments => GetTemplateParametersAsTemplateArguments();

    internal sealed override MethodSymbol originalDefinition => underlyingMethod;

    internal override TypeSymbol receiverType => containingType;

    internal sealed override MethodSymbol reducedFrom => originalDefinition.reducedFrom;

    internal sealed override Symbol containingSymbol => containingType;

    internal sealed override TypeWithAnnotations returnTypeWithAnnotations {
        get {
            if (_lazyReturnType is null) {
                var returnType = templateSubstitution.SubstituteType(originalDefinition.returnTypeWithAnnotations);
                Interlocked.CompareExchange(ref _lazyReturnType, returnType.type, null);
            }

            return _lazyReturnType;
        }
    }

    internal sealed override ImmutableArray<ParameterSymbol> parameters {
        get {
            if (_lazyParameters.IsDefault)
                ImmutableInterlocked.InterlockedInitialize(ref _lazyParameters, SubstituteParameters());

            return _lazyParameters;
        }
    }

    internal sealed override OverriddenOrHiddenMembersResult overriddenOrHiddenMembers {
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

    internal override NamedTypeSymbol containingType { get; }

    internal override MethodSymbol constructedFrom { get; }

    private void EnsureMapAndTemplateParameters() {
        if (!_lazyTemplateParameters.IsDefault)
            return;

        var newMap = _inputMap.WithAlphaRename(originalDefinition, this, out var typeParameters);
        var previousMap = Interlocked.CompareExchange(ref _lazyMap, newMap, null);

        if (previousMap is not null)
            typeParameters = previousMap.SubstituteTemplateParameters(originalDefinition.templateParameters);

        ImmutableInterlocked.InterlockedCompareExchange(ref _lazyTemplateParameters, typeParameters, default);
    }

    private ImmutableArray<ParameterSymbol> SubstituteParameters() {
        var unsubstitutedParameters = originalDefinition.parameters;
        var count = unsubstitutedParameters.Length;

        if (count == 0) {
            return [];
        } else {
            var substituted = ArrayBuilder<ParameterSymbol>.GetInstance(count);
            var map = templateSubstitution;

            foreach (var p in unsubstitutedParameters)
                substituted.Add(new SubstitutedParameterSymbol(this, map, p));

            return substituted.ToImmutableAndFree();
        }
    }

    private int ComputeHashCode() {
        var code = originalDefinition.GetHashCode();
        int containingHashCode;

        containingHashCode = containingType.GetHashCode();

        if (containingHashCode == originalDefinition.containingType.GetHashCode() &&
            WasConstructedForAnnotations(this)) {
            return code;
        }

        code = Hash.Combine(containingHashCode, code);

        if ((object)constructedFrom != this) {
            foreach (var arg in templateArguments)
                code = Hash.Combine(arg.GetHashCode(), code);
        }

        if (code == 0)
            code++;

        return code;

        static bool WasConstructedForAnnotations(SubstitutedMethodSymbol method) {
            var templateArguments = method.templateArguments;
            var templateParameters = method.originalDefinition.templateParameters;

            for (var i = 0; i < templateArguments.Length; i++) {
                if (!templateParameters[i].Equals(
                        templateArguments[i].type.type,
                        TypeCompareKind.ConsiderEverything)) {
                    return false;
                }
            }

            return true;
        }
    }

    internal sealed override bool Equals(Symbol obj, TypeCompareKind compareKind) {
        if (obj is not MethodSymbol other)
            return false;

        if ((object)originalDefinition != other.originalDefinition &&
            originalDefinition != other.originalDefinition) {
            return false;
        }

        if (!TypeSymbol.Equals(containingType, other.containingType, compareKind))
            return false;

        var selfIsDeclaration = (object)this == constructedFrom;
        var otherIsDeclaration = (object)other == other.constructedFrom;

        if (selfIsDeclaration | otherIsDeclaration)
            return selfIsDeclaration & otherIsDeclaration;

        var arity = this.arity;

        for (var i = 0; i < arity; i++) {
            if (!templateArguments[i].Equals(other.templateArguments[i], compareKind))
                return false;
        }

        return true;
    }

    public override int GetHashCode() {
        var code = _hashCode;

        if (code == 0) {
            code = ComputeHashCode();
            _hashCode = code;
        }

        return code;
    }
}
