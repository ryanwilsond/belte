using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text;
using Buckle.CodeAnalysis.Binding;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class NamedTypeSymbol : TypeSymbol, ITypeSymbolWithMembers, ISymbolWithTemplates {
    private string _signature = null;

    public abstract override string name { get; }

    public override SymbolKind kind => SymbolKind.NamedType;

    public abstract ImmutableArray<TemplateParameterSymbol> templateParameters { get; }

    public abstract ImmutableArray<BoundExpression> templateConstraints { get; }

    public abstract ImmutableArray<TypeOrConstant> templateArguments { get; }

    public virtual TemplateMap templateSubstitution { get; } = null;

    internal abstract IEnumerable<string> memberNames { get; }

    internal abstract NamedTypeSymbol constructedFrom { get; }

    internal abstract override Accessibility accessibility { get; }

    internal abstract int arity { get; }

    internal ImmutableArray<MethodSymbol> constructors => GetConstructors();

    internal TypeWithAnnotations typeWithAnnotations { get; private set; }

    internal virtual bool isUnboundTemplateType => false;

    internal new virtual NamedTypeSymbol originalDefinition => this;

    private protected sealed override TypeSymbol _originalTypeSymbolDefinition => originalDefinition;

    internal abstract override ImmutableArray<Symbol> GetMembers();

    internal abstract override ImmutableArray<Symbol> GetMembers(string name);

    internal abstract NamedTypeSymbol GetDeclaredBaseType(ConsList<TypeSymbol> basesBeingResolved);

    internal abstract ImmutableArray<NamedTypeSymbol> GetDeclaredInterfaces(ConsList<TypeSymbol> basesBeingResolved);

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

    // TODO Double check this is needed
    public string Signature() {
        if (_signature is null)
            GenerateSignature();

        return _signature;
    }

    private void GenerateSignature() {
        var signature = new StringBuilder($"{name}<");
        var isFirst = true;

        foreach (var parameter in templateParameters) {
            if (isFirst)
                isFirst = false;
            else
                signature.Append(", ");

            signature.Append(parameter);
        }

        signature.Append('>');
        _signature = signature.ToString();
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
