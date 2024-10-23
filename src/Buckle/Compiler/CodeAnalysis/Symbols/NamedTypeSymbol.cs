using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using Buckle.CodeAnalysis.Binding;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class NamedTypeSymbol : TypeSymbol, ITypeSymbolWithMembers, ISymbolWithTemplates {
    private protected bool _hasNoBaseCycles;

    public abstract override string name { get; }

    public override string metadataName
        => mangleName ? MetadataHelpers.ComposeSuffixedMetadataName(name, templateParameters) : name;

    public override SymbolKind kind => SymbolKind.NamedType;

    public abstract ImmutableArray<TemplateParameterSymbol> templateParameters { get; }

    public abstract ImmutableArray<BoundExpression> templateConstraints { get; }

    public abstract ImmutableArray<TypeOrConstant> templateArguments { get; }

    public virtual TemplateMap templateSubstitution { get; } = null;

    internal abstract bool mangleName { get; }

    internal abstract IEnumerable<string> memberNames { get; }

    internal abstract NamedTypeSymbol constructedFrom { get; }

    internal abstract override Accessibility declaredAccessibility { get; }

    internal abstract int arity { get; }

    internal ImmutableArray<MethodSymbol> constructors => GetConstructors();

    internal virtual bool isUnboundTemplateType => false;

    internal new virtual NamedTypeSymbol originalDefinition => this;

    private protected sealed override TypeSymbol _originalTypeSymbolDefinition => originalDefinition;

    internal override bool isObjectType => originalDefinition.specialType.IsObjectType();

    internal override bool isPrimitiveType => originalDefinition.specialType.IsPrimitiveType();

    internal abstract override ImmutableArray<Symbol> GetMembers();

    internal abstract override ImmutableArray<Symbol> GetMembers(string name);

    internal abstract override ImmutableArray<NamedTypeSymbol> GetTypeMembers();

    internal abstract override ImmutableArray<NamedTypeSymbol> GetTypeMembers(ReadOnlyMemory<char> name);

    internal abstract NamedTypeSymbol GetDeclaredBaseType(ConsList<TypeSymbol> basesBeingResolved);

    internal bool knownToHaveNoDeclaredBaseCycles => _hasNoBaseCycles;

    internal virtual NamedTypeSymbol AsMember(NamedTypeSymbol newOwner) {
        return newOwner.isDefinition
            ? this
            : new SubstitutedNestedTypeSymbol((SubstitutedNamedTypeSymbol)newOwner, this);
    }

    internal ImmutableArray<TypeOrConstant> GetTemplateParametersAsTemplateArguments() {
        return TemplateMap.TemplateParametersAsTypeOrConstants(templateParameters);
    }

    internal NamedTypeSymbol ConstructIfGeneric(ImmutableArray<TypeOrConstant> templateArguments) {
        return templateParameters.IsEmpty ? this : Construct(templateArguments, unbound: false);
    }

    internal static readonly Func<TypeOrConstant, bool> TypeOrConstantIsNullFunction = type
        => type.isType && type.type.type is null;

    internal NamedTypeSymbol Construct(ImmutableArray<TypeOrConstant> templateArguments, bool unbound) {
        if (!ReferenceEquals(this, constructedFrom))
            throw new InvalidOperationException("Cannot create constructed from constructed");

        if (arity == 0)
            throw new InvalidOperationException("Cannot create constructed from non-template");

        if (templateArguments.IsDefault)
            throw new ArgumentNullException(nameof(templateArguments));

        if (templateArguments.Any(TypeOrConstantIsNullFunction))
            throw new ArgumentException("Type argument cannot be null", nameof(templateArguments));

        if (templateArguments.Length != arity)
            throw new ArgumentException("Wrong number of template arguments", nameof(templateArguments));

        if (ConstructedNamedTypeSymbol.TemplateParametersMatchTemplateArguments(templateParameters, templateArguments))
            return this;

        return ConstructCore(templateArguments, unbound);
    }

    private protected virtual NamedTypeSymbol ConstructCore(
        ImmutableArray<TypeOrConstant> templateArguments,
        bool unbound) {
        return new ConstructedNamedTypeSymbol(this, templateArguments, unbound);
    }

    internal int ComputeHashCode() {
        if (WasConstructedForAnnotations(this))
            return originalDefinition.GetHashCode();

        var code = originalDefinition.GetHashCode();
        code = Hash.Combine(containingType, code);

        if ((object)constructedFrom != this) {
            foreach (var arg in templateArguments)
                code = Hash.Combine(arg, code);
        }

        if (code == 0)
            code++;

        return code;

        static bool WasConstructedForAnnotations(NamedTypeSymbol type) {
            do {
                var typeArguments = type.templateArguments;
                var typeParameters = type.originalDefinition.templateParameters;

                for (var i = 0; i < typeArguments.Length; i++) {
                    if (!typeParameters[i].Equals(
                             typeArguments[i].type.type.originalDefinition,
                             TypeCompareKind.ConsiderEverything)) {
                        return false;
                    }
                }

                type = type.containingType;
            } while (type is not null && !type.isDefinition);

            return true;
        }
    }

    internal override bool Equals(TypeSymbol other, TypeCompareKind compareKind) {
        if ((object)other == this)
            return true;

        if (other is null)
            return false;

        var otherAsType = other as NamedTypeSymbol;

        if (other is null) return false;

        var thisOriginalDefinition = originalDefinition;
        var otherOriginalDefinition = other.originalDefinition;

        var thisIsOriginalDefinition = (object)this == thisOriginalDefinition;
        var otherIsOriginalDefinition = (object)other == otherOriginalDefinition;

        if (thisIsOriginalDefinition && otherIsOriginalDefinition)
            return false;

        if ((thisIsOriginalDefinition || otherIsOriginalDefinition) &&
            (compareKind & (TypeCompareKind.IgnoreArraySizesAndLowerBounds | TypeCompareKind.IgnoreNullability)) == 0) {
            return false;
        }

        if (!Equals(thisOriginalDefinition, otherOriginalDefinition, compareKind))
            return false;

        return EqualsComplicatedCases(otherAsType, compareKind);
    }

    public override int GetHashCode() {
        if (specialType == SpecialType.Object)
            return (int)SpecialType.Object;

        return RuntimeHelpers.GetHashCode(originalDefinition);
    }

    private ImmutableArray<MethodSymbol> GetConstructors() {
        var candidates = GetMembers(WellKnownMemberNames.InstanceConstructorName);

        if (candidates.IsEmpty)
            return [];

        var constructors = ArrayBuilder<MethodSymbol>.GetInstance();

        foreach (var candidate in candidates) {
            if (candidate is MethodSymbol method)
                constructors.Add(method);
        }

        return constructors.ToImmutableAndFree();
    }

    private bool EqualsComplicatedCases(NamedTypeSymbol other, TypeCompareKind compareKind) {
        if (containingType is not null &&
            !containingType.Equals(other.containingType, compareKind)) {
            return false;
        }

        var thisIsNotConstructed = ReferenceEquals(constructedFrom, this);
        var otherIsNotConstructed = ReferenceEquals(other.constructedFrom, other);

        if (thisIsNotConstructed && otherIsNotConstructed)
            return true;

        if (isUnboundTemplateType != other.isUnboundTemplateType)
            return false;

        if ((thisIsNotConstructed || otherIsNotConstructed) &&
            (compareKind & (TypeCompareKind.IgnoreArraySizesAndLowerBounds | TypeCompareKind.IgnoreNullability)) == 0) {
            return false;
        }

        var thisTemplateArguments = templateArguments;
        var otherTemplateArguments = other.templateArguments;
        var count = thisTemplateArguments.Length;

        for (var i = 0; i < count; i++) {
            var templateArgument = thisTemplateArguments[i];
            var otherTemplateArgument = otherTemplateArguments[i];
            if (!templateArgument.Equals(otherTemplateArgument, compareKind))
                return false;
        }

        return true;
    }
}
