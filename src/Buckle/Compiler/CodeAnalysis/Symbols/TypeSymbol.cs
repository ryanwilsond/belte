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
