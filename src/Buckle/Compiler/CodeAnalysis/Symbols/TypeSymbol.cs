using System;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// A type symbol. This is just the base type name, not a full <see cref="Binding.BoundType" />.
/// </summary>
internal abstract class TypeSymbol : NamespaceOrTypeSymbol, ITypeSymbol {
    public override SymbolKind kind => SymbolKind.NamedType;

    internal new TypeSymbol originalDefinition => _originalTypeSymbolDefinition;

    private protected virtual TypeSymbol _originalTypeSymbolDefinition => this;

    private protected sealed override Symbol _originalSymbolDefinition => _originalTypeSymbolDefinition;

    internal abstract NamedTypeSymbol baseType { get; }

    internal abstract TypeKind typeKind { get; }

    internal abstract bool isPrimitiveType { get; }

    internal abstract bool isObjectType { get; }

    internal virtual SpecialType specialType => SpecialType.None;

    internal TypeSymbol EffectiveType() {
        return typeKind == TypeKind.TemplateParameter ? ((TemplateParameterSymbol)this).effectiveBaseClass : this;
    }

    internal bool IsDerivedFrom(TypeSymbol type, TypeCompareKind compareKind) {
        if ((object)this == type)
            return false;

        var current = baseType;

        while ((object)current is not null) {
            if (type.Equals(current, compareKind))
                return true;

            current = current.baseType;
        }

        return false;
    }

    internal bool IsEqualToOrDerivedFrom(TypeSymbol type, TypeCompareKind compareKind) {
        return Equals(type, compareKind) || IsDerivedFrom(type, compareKind);
    }

    internal int TypeToIndex() {
        switch (specialType) {
            case SpecialType.Any: return 0;
            case SpecialType.String: return 1;
            case SpecialType.Bool: return 2;
            case SpecialType.Char: return 3;
            case SpecialType.Int: return 4;
            case SpecialType.Decimal: return 5;
            case SpecialType.Type: return 6;
            case SpecialType.Nullable:
                var underlyingType = GetNullableUnderlyingType();

                switch (underlyingType.specialType) {
                    case SpecialType.Any: return 7;
                    case SpecialType.String: return 8;
                    case SpecialType.Bool: return 9;
                    case SpecialType.Char: return 10;
                    case SpecialType.Int: return 11;
                    case SpecialType.Decimal: return 12;
                    case SpecialType.Type: return 13;
                }

                goto default;
            default: return -1;
        }
    }

    internal bool IsNullableType() {
        return originalDefinition.specialType == SpecialType.Nullable;
    }

    internal bool IsErrorType() {
        return kind == SymbolKind.ErrorType;
    }

    internal bool IsClassType() {
        return typeKind == TypeKind.Class;
    }

    internal bool IsStructType() {
        return typeKind == TypeKind.Struct;
    }

    internal bool IsTemplateParameter() {
        return typeKind == TypeKind.TemplateParameter;
    }

    internal bool IsPrimitiveType() {
        return specialType.IsPrimitiveType();
    }

    internal bool IsVoidType() {
        // TODO Use originalDefinition here?
        return specialType == SpecialType.Void;
    }

    internal bool IsAtLeastAsVisibleAs(Symbol symbol) {
        return typeKind switch {
            TypeKind.Class or TypeKind.Struct => symbol.declaredAccessibility switch {
                Accessibility.Public => declaredAccessibility is Accessibility.Public,
                Accessibility.Protected => declaredAccessibility is Accessibility.Public or Accessibility.Protected,
                _ => true,
            },
            _ => true,
        };
    }

    internal bool IsPossiblyNullableTypeTemplateParameter() {
        return this is TemplateParameterSymbol t && t.underlyingType.isNullable;
    }

    internal TypeSymbol GetNullableUnderlyingType() {
        return GetNullableUnderlyingTypeWithAnnotations().type;
    }

    internal TypeWithAnnotations GetNullableUnderlyingTypeWithAnnotations() {
        return ((NamedTypeSymbol)this).templateArguments[0].type;
    }

    internal TypeSymbol StrippedType() {
        return IsNullableType() ? GetNullableUnderlyingType() : this;
    }

    internal bool InheritsFromIgnoringConstruction(NamedTypeSymbol baseType) {
        var current = this;

        while (current is not null) {
            if (current == (object)baseType)
                return true;

            current = current.baseType?.originalDefinition;
        }

        return false;
    }

    internal virtual bool Equals(TypeSymbol other, TypeCompareKind compareKind) {
        return ReferenceEquals(this, other);
    }

    internal sealed override bool Equals(Symbol other, TypeCompareKind compareKind) {
        if (other is not TypeSymbol otherAsType)
            return false;

        return Equals(otherAsType, compareKind);
    }

    internal static bool Equals(TypeSymbol left, TypeSymbol right, TypeCompareKind compareKind) {
        if (left is null)
            return right is null;

        return left.Equals(right, compareKind);
    }

    public override int GetHashCode() {
        return RuntimeHelpers.GetHashCode(this);
    }

    [Obsolete("Use 'TypeSymbol.Equals(TypeSymbol, TypeSymbol, TypeCompareKind)' method.", true)]
    public static bool operator ==(TypeSymbol left, TypeSymbol right)
        => throw ExceptionUtilities.Unreachable();

    [Obsolete("Use 'TypeSymbol.Equals(TypeSymbol, TypeSymbol, TypeCompareKind)' method.", true)]
    public static bool operator !=(TypeSymbol left, TypeSymbol right)
        => throw ExceptionUtilities.Unreachable();

    [Obsolete("Use 'TypeSymbol.Equals(TypeSymbol, TypeSymbol, TypeCompareKind)' method.", true)]
    public static bool operator ==(Symbol left, TypeSymbol right)
        => throw ExceptionUtilities.Unreachable();

    [Obsolete("Use 'TypeSymbol.Equals(TypeSymbol, TypeSymbol, TypeCompareKind)' method.", true)]
    public static bool operator !=(Symbol left, TypeSymbol right)
        => throw ExceptionUtilities.Unreachable();

    [Obsolete("Use 'TypeSymbol.Equals(TypeSymbol, TypeSymbol, TypeCompareKind)' method.", true)]
    public static bool operator ==(TypeSymbol left, Symbol right)
        => throw ExceptionUtilities.Unreachable();

    [Obsolete("Use 'TypeSymbol.Equals(TypeSymbol, TypeSymbol, TypeCompareKind)' method.", true)]
    public static bool operator !=(TypeSymbol left, Symbol right)
        => throw ExceptionUtilities.Unreachable();

    public ImmutableArray<ISymbol> GetMembersPublic() {
        return [.. GetMembers()];
    }
}
