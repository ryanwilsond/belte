using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// A type symbol. This is just the base type name, not a full <see cref="Binding.BoundType" />.
/// </summary>
internal abstract class TypeSymbol : Symbol, ITypeSymbol {
    protected List<Symbol> _lazyMembers;
    protected Dictionary<string, ImmutableArray<Symbol>> _lazyMembersDictionary;

    public override SymbolKind kind => SymbolKind.Type;

    internal new TypeSymbol originalDefinition => originalTypeDefinition;

    internal virtual TypeSymbol originalTypeDefinition => this;

    internal override Symbol originalSymbolDefinition => originalTypeDefinition;

    internal abstract NamedTypeSymbol baseType { get; }

    internal abstract TypeKind typeKind { get; }

    internal virtual SpecialType specialType => SpecialType.None;

    internal virtual ImmutableArray<Symbol> members { get; }

    internal TypeSymbol EffectiveType() {
        return typeKind == TypeKind.TemplateParameter ? ((TemplateParameterSymbol)this).EffectiveBaseClass() : this;
    }

    internal virtual ImmutableArray<Symbol> GetMembers(string name) {
        if (_lazyMembersDictionary is null || _lazyMembers is null)
            ConstructLazyMembersDictionary();

        return _lazyMembersDictionary.TryGetValue(name, out var result) ? result : ImmutableArray<Symbol>.Empty;
    }

    internal virtual ImmutableArray<Symbol> GetMembers() {
        if (_lazyMembers is null)
            ConstructLazyMembers();

        return _lazyMembers.ToImmutableArray();
    }

    internal bool IsDerivedFrom(TypeSymbol type, TypeCompareKind compareKind) {
        if (other is null)
            return false;

        if (this == other)
            return true;

        if (typeKind != other.typeKind)
            return false;

        return InheritsFrom(other.baseType);
    }

    internal bool IsEqualToOrDerivedFrom(TypeSymbol type, TypeCompareKind compareKind) {
        return Equals(type, compareKind) || IsDerivedFrom(type, compareKind);
    }

    internal override int GetInheritanceDepth(TypeSymbol other) {
        if (!InheritsFrom(other))
            return -1;

        var depth = 0;
        var current = this;

        while (current != other) {
            depth++;
            current = current.baseType;
        }

        return depth;
    }

    public ImmutableArray<ISymbol> GetMembersPublic() {
        if (_lazyMembers is null)
            ConstructLazyMembers();

        return _lazyMembers.ToImmutableArray<ISymbol>();
    }

    public bool Equals(TypeSymbol other) {
        return Equals(other, TypeCompareKind.ConsiderEverything);
    }

    public bool Equals(TypeSymbol other, TypeCompareKind compareKind) {
        if (compareKind == TypeCompareKind.ConsiderEverything)
            return isNullable == other.isNullable && underlyingType == other.underlyingType;

        if (compareKind == TypeCompareKind.IgnoreNullability)
            return underlyingType == other.underlyingType;

        return false;
    }

    protected virtual void ConstructLazyMembers() {
        _lazyMembers = members.ToList();
    }

    private void ConstructLazyMembersDictionary() {
        if (_lazyMembers is null)
            ConstructLazyMembers();

        _lazyMembersDictionary = _lazyMembers.ToImmutableArray()
            .ToDictionary(m => m.name, StringOrdinalComparer.Instance);
    }
}
