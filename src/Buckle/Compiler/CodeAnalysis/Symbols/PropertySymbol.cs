using System.Collections.Immutable;
using System.Linq;
using Buckle.CodeAnalysis.Binding;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class PropertySymbol : Symbol, IPropertySymbol {
    private ParameterSignature _lazyParameterSignature;

    internal PropertySymbol() { }

    public new virtual PropertySymbol originalDefinition => this;

    private protected sealed override Symbol _originalSymbolDefinition => originalDefinition;

    internal bool returnsByRef => refKind == RefKind.Ref;

    internal abstract RefKind refKind { get; }

    internal abstract TypeWithAnnotations typeWithAnnotations { get; }

    internal TypeSymbol type => typeWithAnnotations.type;

    internal abstract ImmutableArray<ParameterSymbol> parameters { get; }

    internal int parameterCount => parameters.Length;

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

    internal virtual bool requiresInstanceReceiver => !isStatic;

    internal bool isReadOnly => GetOwnOrInheritedSetMethod() is null;

    internal bool isWriteOnly => GetOwnOrInheritedGetMethod() is null;

    internal abstract bool hasSpecialName { get; }

    internal abstract MethodSymbol getMethod { get; }

    internal abstract MethodSymbol setMethod { get; }

    internal abstract CallingConvention callingConvention { get; }

    internal abstract bool mustCallMethodsDirectly { get; }

    public PropertySymbol overriddenProperty {
        get {
            if (isOverride) {
                if (isDefinition)
                    return (PropertySymbol)overriddenOrHiddenMembers.GetOverriddenMember();

                return (PropertySymbol)OverriddenOrHiddenMembersResult.GetOverriddenMember(
                    this,
                    originalDefinition.overriddenProperty
                );
            }

            return null;
        }
    }

    internal virtual OverriddenOrHiddenMembersResult overriddenOrHiddenMembers => this.MakeOverriddenOrHiddenMembers();

    internal bool hidesBasePropertiesByName {
        get {
            var accessor = getMethod ?? setMethod;
            return accessor is not null && accessor.hidesBaseMethodsByName;
        }
    }

    internal PropertySymbol GetLeastOverriddenProperty(NamedTypeSymbol accessingTypeOpt) {
        accessingTypeOpt = accessingTypeOpt?.originalDefinition;
        var p = this;

        while (p.isOverride && !p.hidesBasePropertiesByName) {
            var overridden = p.overriddenProperty;

            if (overridden is null ||
                (accessingTypeOpt is { } && !AccessCheck.IsSymbolAccessible(overridden, accessingTypeOpt))) {
                break;
            }

            p = overridden;
        }

        return p;
    }

    internal virtual bool isExplicitInterfaceImplementation => explicitInterfaceImplementations.Any();

    internal abstract ImmutableArray<PropertySymbol> explicitInterfaceImplementations { get; }

    public sealed override SymbolKind kind => SymbolKind.Property;

    internal override TResult Accept<TArgument, TResult>(SymbolVisitor<TArgument, TResult> visitor, TArgument argument) {
        return visitor.VisitProperty(this, argument);
    }

    internal override void Accept(SymbolVisitor visitor) {
        visitor.VisitProperty(this);
    }

    internal PropertySymbol AsMember(NamedTypeSymbol newOwner) {
        return newOwner.isDefinition
            ? this
            : new SubstitutedPropertySymbol(newOwner as SubstitutedNamedTypeSymbol, this);
    }

    internal MethodSymbol GetOwnOrInheritedSetMethod() {
        var current = this;

        while (current is not null) {
            var setMethod = current.setMethod;

            if (setMethod is not null)
                return setMethod;

            current = current.overriddenProperty;
        }

        return null;
    }

    internal MethodSymbol GetOwnOrInheritedGetMethod() {
        var current = this;

        while (current is not null) {
            var getMethod = current.getMethod;

            if (getMethod is not null)
                return getMethod;

            current = current.overriddenProperty;
        }

        return null;
    }

    internal override bool Equals(Symbol symbol, TypeCompareKind compareKind) {
        if (symbol is not PropertySymbol other)
            return false;

        if (ReferenceEquals(this, other))
            return true;

        return TypeSymbol.Equals(containingType, other.containingType, compareKind) &&
            ReferenceEquals(originalDefinition, other.originalDefinition);
    }

    public override int GetHashCode() {
        var hash = 1;
        hash = Hash.Combine(containingType, hash);
        hash = Hash.Combine(name, hash);
        hash = Hash.Combine(hash, parameterCount);
        return hash;
    }

    ITypeSymbol IPropertySymbol.type => type;
}
