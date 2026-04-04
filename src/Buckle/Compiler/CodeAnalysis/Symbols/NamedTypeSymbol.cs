using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using Buckle.CodeAnalysis.Binding;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class NamedTypeSymbol : TypeSymbol, INamedTypeSymbol, ISymbolWithTemplates {
    private protected bool _hasNoBaseCycles;

    public abstract override string name { get; }

    public override string metadataName
        => mangleName ? MetadataHelpers.ComposeSuffixedMetadataName(name, arity) : name;

    public override SymbolKind kind => SymbolKind.NamedType;

    public abstract ImmutableArray<TemplateParameterSymbol> templateParameters { get; }

    public abstract ImmutableArray<BoundExpression> templateConstraints { get; }

    public abstract ImmutableArray<TypeOrConstant> templateArguments { get; }

    public virtual TemplateMap templateSubstitution { get; } = null;

    public abstract int arity { get; }

    public override bool isObjectType
        => !isPrimitiveType && !IsStructType() && !TypeSymbolExtensions.IsPointerOrFunctionPointer(this);

    public override bool isPrimitiveType => originalDefinition.specialType.IsPrimitiveType();

    public bool isTemplateType {
        get {
            for (var current = this; !ReferenceEquals(current, null); current = current.containingType) {
                if (current.templateArguments.Length != 0)
                    return true;
            }

            return false;
        }
    }

    internal virtual FieldSymbol fixedElementField => null;

    internal abstract bool mangleName { get; }

    internal abstract IEnumerable<string> memberNames { get; }

    internal abstract NamedTypeSymbol constructedFrom { get; }

    internal abstract override Accessibility declaredAccessibility { get; }

    internal ImmutableArray<MethodSymbol> instanceConstructors => GetConstructors(true, false);

    internal ImmutableArray<MethodSymbol> staticConstructors => GetConstructors(false, true);

    internal virtual bool isUnboundTemplateType => false;

    internal new virtual NamedTypeSymbol originalDefinition => this;

    private protected sealed override TypeSymbol _originalTypeSymbolDefinition => originalDefinition;

    internal abstract override ImmutableArray<Symbol> GetMembers();

    internal abstract override ImmutableArray<Symbol> GetMembers(string name);

    internal abstract override ImmutableArray<NamedTypeSymbol> GetTypeMembers();

    internal abstract override ImmutableArray<NamedTypeSymbol> GetTypeMembers(ReadOnlyMemory<char> name);

    internal abstract NamedTypeSymbol GetDeclaredBaseType(ConsList<TypeSymbol> basesBeingResolved);

    internal bool knownToHaveNoDeclaredBaseCycles => _hasNoBaseCycles;

    internal virtual NamedTypeSymbol enumUnderlyingType => null;

    internal override void Accept(SymbolVisitor visitor) {
        visitor.VisitNamedType(this);
    }

    internal override TResult Accept<TArgument, TResult>(
        SymbolVisitor<TArgument, TResult> visitor,
        TArgument argument) {
        return visitor.VisitNamedType(this, argument);
    }

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

    internal void SetKnownToHaveNoDeclaredBaseCycles() {
        _hasNoBaseCycles = true;
    }

    internal void AddOperators(string name, ArrayBuilder<MethodSymbol> operators) {
        var candidates = GetSimpleNonTypeMembers(name);

        if (candidates.IsEmpty)
            return;

        foreach (var candidate in candidates) {
            if (candidate is MethodSymbol { methodKind: MethodKind.Operator } method)
                operators.Add(method);
        }
    }

    internal new TemplateParameterSymbol FindEnclosingTemplateParameter(string name) {
        var allTemplateParameters = ArrayBuilder<TemplateParameterSymbol>.GetInstance();
        GetAllTypeParameters(allTemplateParameters);

        TemplateParameterSymbol result = null;

        foreach (var enclosingTemplateParameter in allTemplateParameters) {
            if (name == enclosingTemplateParameter.name) {
                result = enclosingTemplateParameter;
                break;
            }
        }

        allTemplateParameters.Free();
        return result;
    }

    internal virtual ImmutableArray<Symbol> GetSimpleNonTypeMembers(string name) {
        return GetMembers(name);
    }

    internal ImmutableArray<MethodSymbol> GetOperators(string name) {
        var candidates = GetSimpleNonTypeMembers(name);

        if (candidates.IsEmpty)
            return [];

        var operators = ArrayBuilder<MethodSymbol>.GetInstance(candidates.Length);

        foreach (var candidate in candidates) {
            if (candidate is MethodSymbol { methodKind: MethodKind.Operator or MethodKind.Conversion } method)
                operators.Add(method);
        }

        return operators.ToImmutableAndFree();
    }

    internal void GetAllTypeArguments(ref TemporaryArray<TypeSymbol> builder) {
        var outer = containingType;
        outer?.GetAllTypeArguments(ref builder);

        foreach (var argument in templateArguments)
            builder.Add(argument.type.type);
    }

    internal void GetAllTypeParameters(ArrayBuilder<TemplateParameterSymbol> result) {
        containingType?.GetAllTypeParameters(result);
        result.AddRange(templateParameters);
    }

    internal ImmutableArray<TemplateParameterSymbol> GetAllTypeParameters() {
        if (containingType is null)
            return templateParameters;

        var builder = ArrayBuilder<TemplateParameterSymbol>.GetInstance();
        GetAllTypeParameters(builder);
        return builder.ToImmutableAndFree();
    }

    internal static readonly Func<TypeOrConstant, bool> TypeOrConstantIsNullFunction = type
        => type.isType && type.type.type is null;

    internal NamedTypeSymbol Construct(ImmutableArray<TypeOrConstant> templateArguments, bool unbound = false) {
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

    internal override bool ApplyNullableTransforms(
        byte defaultTransformFlag,
        ImmutableArray<byte> transforms,
        ref int position,
        out TypeSymbol result) {
        if (!isTemplateType) {
            result = this;
            return true;
        }

        var allTypeArguments = ArrayBuilder<TypeOrConstant>.GetInstance();
        GetAllTypeArguments(allTypeArguments);

        var haveChanges = false;

        for (var i = 0; i < allTypeArguments.Count; i++) {
            var oldTypeArgument = allTypeArguments[i].type;

            if (!oldTypeArgument.ApplyNullableTransforms(defaultTransformFlag, transforms, ref position, out var newTypeArgument)) {
                allTypeArguments.Free();
                result = this;
                return false;
            } else if (!oldTypeArgument.IsSameAs(newTypeArgument)) {
                allTypeArguments[i] = new TypeOrConstant(newTypeArgument);
                haveChanges = true;
            }
        }

        result = haveChanges ? WithTypeArguments(allTypeArguments.ToImmutable()) : this;
        allTypeArguments.Free();
        return true;
    }

    internal NamedTypeSymbol WithTypeArguments(ImmutableArray<TypeOrConstant> allTypeArguments) {
        var definition = originalDefinition;
        var substitution = new TemplateMap(definition.GetAllTypeParameters(), allTypeArguments);
        return substitution.SubstituteNamedType(definition);
    }

    internal void GetAllTypeArguments(ArrayBuilder<TypeOrConstant> builder) {
        containingType?.GetAllTypeArguments(builder);
        builder.AddRange(templateArguments);
    }

    internal NamedTypeSymbol AsUnboundTemplateType() {
        if (!isTemplateType)
            throw new InvalidOperationException();

        var original = originalDefinition;
        var n = original.arity;
        var originalContainingType = original.containingType;

        var constructedFrom = (originalContainingType is null)
            ? original
            : original.AsMember(
                originalContainingType.isTemplateType
                    ? originalContainingType.AsUnboundTemplateType()
                    : originalContainingType
                );

        if (n == 0)
            return constructedFrom;

        var templateArguments = UnboundArgumentErrorTypeSymbol.CreateTemplateArguments(
            constructedFrom.templateParameters,
            n,
            null /* TODO error */
        );

        return constructedFrom.Construct(templateArguments, true);
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
                    if (typeArguments[i].isConstant)
                        return false;

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
        if ((compareKind & TypeCompareKind.IgnoreNullability) != 0 && !this.IsNullableType())
            other = other.StrippedType();

        if ((object)other == this)
            return true;

        if (other is null)
            return false;

        var otherAsType = other as NamedTypeSymbol;

        if (other is null)
            return false;

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

        if ((compareKind & TypeCompareKind.IgnoreNullability) != 0 && this.IsNullableType())
            return Equals(StrippedType(), other, compareKind);

        if (!Equals(thisOriginalDefinition, otherOriginalDefinition, compareKind))
            return false;

        return EqualsComplicatedCases(otherAsType, compareKind);
    }

    public override int GetHashCode() {
        if (specialType == SpecialType.Object)
            return (int)SpecialType.Object;

        return RuntimeHelpers.GetHashCode(originalDefinition);
    }

    private ImmutableArray<MethodSymbol> GetConstructors(bool includeInstance, bool includeStatic) {
        var instanceCandidates = includeInstance ? GetMembers(WellKnownMemberNames.InstanceConstructorName) : [];
        var staticCandidates = includeStatic ? GetMembers(WellKnownMemberNames.StaticConstructorName) : [];

        if (instanceCandidates.IsEmpty && staticCandidates.IsEmpty)
            return [];

        var constructors = ArrayBuilder<MethodSymbol>.GetInstance();

        foreach (var candidate in instanceCandidates) {
            if (candidate is MethodSymbol method)
                constructors.Add(method);
        }

        foreach (var candidate in staticCandidates) {
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

    ImmutableArray<IMethodSymbol> INamedTypeSymbol.constructors
        => GetConstructors(true, true).Cast<MethodSymbol, IMethodSymbol>();
}
