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

    internal new TypeSymbol originalDefinition => _originalTypeSymbolDefinition;

    protected virtual TypeSymbol _originalTypeSymbolDefinition => this;

    protected sealed override Symbol _originalSymbolDefinition => _originalTypeSymbolDefinition;

    internal abstract NamedTypeSymbol baseType { get; }

    internal abstract TypeKind typeKind { get; }

    internal abstract bool isRef { get; }

    internal abstract bool isConst { get; }

    internal virtual SpecialType specialType => SpecialType.None;

    internal virtual ImmutableArray<Symbol> members { get; }

    internal TypeSymbol EffectiveType() {
        return typeKind == TypeKind.TemplateParameter ? ((TemplateParameterSymbol)this).effectiveBaseClass : this;
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

    public ImmutableArray<ISymbol> GetMembersPublic() {
        if (_lazyMembers is null)
            ConstructLazyMembers();

        return _lazyMembers.ToImmutableArray<ISymbol>();
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
