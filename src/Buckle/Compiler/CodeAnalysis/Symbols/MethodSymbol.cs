using System;
using System.Collections.Immutable;
using System.Linq;
using Buckle.CodeAnalysis.Binding;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// Represents a method or method-like symbol (including constructor or operator).
/// </summary>
internal abstract class MethodSymbol : Symbol, ISymbolWithTemplates {
    private ParameterSignature _lazyParameterSignature;

    private protected MethodSymbol() { }

    public override SymbolKind kind => SymbolKind.Method;

    public abstract ImmutableArray<TemplateParameterSymbol> templateParameters { get; }

    public abstract ImmutableArray<BoundExpression> templateConstraints { get; }

    public abstract ImmutableArray<TypeOrConstant> templateArguments { get; }

    public virtual TemplateMap templateSubstitution => null;

    internal abstract RefKind refKind { get; }

    internal abstract bool returnsVoid { get; }

    internal abstract TypeWithAnnotations returnTypeWithAnnotations { get; }

    internal abstract MethodKind methodKind { get; }

    internal abstract int arity { get; }

    internal abstract ImmutableArray<ParameterSymbol> parameters { get; }

    internal abstract bool isConst { get; }

    internal abstract bool hidesBaseMethodsByName { get; }

    internal abstract bool hasSpecialName { get; }

    internal virtual int parameterCount => parameters.Length;

    internal virtual MethodSymbol constructedFrom => this;

    internal virtual bool requiresInstanceReceiver => !isStatic;

    internal virtual bool isTemplateMethod => arity != 0;

    internal virtual OverriddenOrHiddenMembersResult overriddenOrHiddenMembers => this.MakeOverriddenOrHiddenMembers();

    internal virtual MethodSymbol reducedFrom => null;

    internal virtual TypeSymbol receiverType => containingType;

    internal new virtual MethodSymbol originalDefinition => this;

    internal new bool isDefinition => (object)this == originalDefinition;

    private protected sealed override Symbol _originalSymbolDefinition => originalDefinition;

    internal TypeSymbol returnType => returnTypeWithAnnotations.type;

    internal bool returnsByRef => refKind == RefKind.Ref;

    internal bool returnsByRefConst => refKind == RefKind.RefConst;

    internal bool isEntryPointCandidate
        => isStatic && !isAbstract && !isVirtual && name == WellKnownMemberNames.EntryPointMethodName;

    internal ImmutableArray<TypeWithAnnotations> parameterTypesWithAnnotations {
        get {
            ParameterSignature.PopulateParameterSignature(parameters, ref _lazyParameterSignature);
            return _lazyParameterSignature.parameterTypesWithAnnotations;
        }
    }

    internal ImmutableArray<RefKind> parameterRefKinds {
        get {
            ParameterSignature.PopulateParameterSignature(parameters, ref _lazyParameterSignature);
            return _lazyParameterSignature.parameterRefKinds;
        }
    }

    internal MethodSymbol overriddenMethod {
        get {
            if (isOverride && ReferenceEquals(constructedFrom, this)) {
                if (isDefinition)
                    return (MethodSymbol)overriddenOrHiddenMembers.GetOverriddenMember();

                return (MethodSymbol)OverriddenOrHiddenMembersResult.GetOverriddenMember(
                    this,
                    originalDefinition.overriddenMethod
                );
            }

            return null;
        }
    }

    internal TypeSymbol GetParameterType(int index) {
        return parameterTypesWithAnnotations[index].type;
    }

    internal MethodSymbol Construct(ImmutableArray<TypeOrConstant> templateArguments) {
        if (!ReferenceEquals(this, constructedFrom) || arity == 0)
            throw new InvalidOperationException();

        if (templateArguments.IsDefault)
            throw new ArgumentNullException(nameof(templateArguments));

        if (templateArguments.Any(NamedTypeSymbol.TypeOrConstantIsNullFunction))
            throw new ArgumentException("Type argument cannot be null", nameof(templateArguments));

        if (templateArguments.Length != arity)
            throw new ArgumentException("Wrong number of template arguments", nameof(templateArguments));

        if (ConstructedNamedTypeSymbol.TemplateParametersMatchTemplateArguments(templateParameters, templateArguments))
            return this;

        return new ConstructedMethodSymbol(this, templateArguments);
    }

    internal MethodSymbol AsMember(NamedTypeSymbol newOwner) {
        return newOwner.isDefinition ? this : new SubstitutedMethodSymbol(newOwner, this);
    }

    internal static bool CanOverrideOrHide(MethodKind kind) {
        switch (kind) {
            case MethodKind.Constructor:
            case MethodKind.Builtin:
                return false;
            case MethodKind.LocalFunction:
            case MethodKind.Operator:
            case MethodKind.Ordinary:
                return true;
            default:
                throw ExceptionUtilities.UnexpectedValue(kind);
        }
    }

    internal bool CanBeHiddenByMemberKind(SymbolKind hidingMemberKind) {
        switch (hidingMemberKind) {
            case SymbolKind.ErrorType:
            case SymbolKind.NamedType:
            case SymbolKind.Method:
                return CanBeHiddenByMethodPropertyOrType();
            case SymbolKind.Field:
                return true;
            default:
                throw ExceptionUtilities.UnexpectedValue(hidingMemberKind);
        }
    }

    internal MethodSymbol GetLeastOverriddenMethod(NamedTypeSymbol accessingType) {
        return GetLeastOverriddenMethodCore(accessingType, false);
    }

    internal ImmutableArray<TypeOrConstant> GetTemplateParametersAsTemplateArguments() {
        return TemplateMap.TemplateParametersAsTypeOrConstants(templateParameters);
    }

    internal MethodSymbol GetConstructedLeastOverriddenMethod(
        NamedTypeSymbol accessingType,
        bool requireSameReturnType) {
        var m = constructedFrom.GetLeastOverriddenMethodCore(accessingType, requireSameReturnType);
        return m.isTemplateMethod ? m.Construct(templateArguments) : m;
    }

    private MethodSymbol GetLeastOverriddenMethodCore(NamedTypeSymbol accessingType, bool requireSameReturnType) {
        accessingType = accessingType?.originalDefinition;
        var m = this;

        while (m.isOverride && !m.hidesBaseMethodsByName) {
            var overridden = m.overriddenMethod;

            if (overridden is null ||
                (accessingType is not null && !AccessCheck.IsSymbolAccessible(overridden, accessingType)) ||
                (requireSameReturnType && returnType.Equals(overridden.returnType, TypeCompareKind.AllIgnoreOptions))) {
                break;
            }

            m = overridden;
        }

        return m;
    }

    private bool CanBeHiddenByMethodPropertyOrType() {
        switch (methodKind) {
            case MethodKind.Constructor:
                return false;
            default:
                return true;
        }
    }

    internal override bool Equals(Symbol other, TypeCompareKind compareKind) {
        if (other is SubstitutedMethodSymbol sms)
            return sms.Equals(this, compareKind);

        return base.Equals(other, compareKind);
    }
}
