using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class MergedNamespaceSymbol : NamespaceSymbol {
    private readonly ImmutableArray<NamespaceSymbol> _namespacesToMerge;
    private readonly NamespaceSymbol _containingNamespace;

    private readonly string _nameOpt;
    private readonly CachingDictionary<ReadOnlyMemory<char>, Symbol> _cachedLookup;

    private ImmutableArray<Symbol> _allMembers;

    internal static NamespaceSymbol Create(
        NamespaceExtent extent,
        NamespaceSymbol containingNamespace,
        ImmutableArray<NamespaceSymbol> namespacesToMerge,
        string nameOpt = null) {

        return (namespacesToMerge.Length == 1 && nameOpt == null)
            ? namespacesToMerge[0]
            : new MergedNamespaceSymbol(extent, containingNamespace, namespacesToMerge, nameOpt);
    }

    private MergedNamespaceSymbol(
        NamespaceExtent extent,
        NamespaceSymbol containingNamespace,
        ImmutableArray<NamespaceSymbol> namespacesToMerge,
        string nameOpt) {
        this.extent = extent;
        _namespacesToMerge = namespacesToMerge;
        _containingNamespace = containingNamespace;
        _cachedLookup = new CachingDictionary<ReadOnlyMemory<char>, Symbol>(
            SlowGetChildrenOfName,
            SlowGetChildNames,
            ReadOnlyMemoryOfCharComparer.Instance
        );

        _nameOpt = nameOpt;
    }

    public override string name => _nameOpt ?? _namespacesToMerge[0].name;

    internal override Symbol containingSymbol => _containingNamespace;

    internal override NamespaceExtent extent { get; }

    internal override ImmutableArray<TextLocation> locations
        => _namespacesToMerge.SelectMany(namespaceSymbol => namespaceSymbol.locations).AsImmutable();

    internal override TextLocation location => throw new InvalidOperationException();

    internal override ImmutableArray<SyntaxReference> declaringSyntaxReferences
        => _namespacesToMerge.SelectMany(namespaceSymbol => namespaceSymbol.declaringSyntaxReferences).AsImmutable();

    internal override SyntaxReference syntaxReference => throw new InvalidOperationException();

    internal ImmutableArray<NamespaceSymbol> constituentNamespaces => _namespacesToMerge;

    internal NamespaceSymbol GetConstituentForCompilation(Compilation compilation) {
        foreach (var n in _namespacesToMerge) {
            if (n.IsFromCompilation(compilation))
                return n;
        }

        return null;
    }

    internal override void ForceComplete(TextLocation location) {
        foreach (var part in _namespacesToMerge)
            part.ForceComplete(location);
    }

    private ImmutableArray<Symbol> SlowGetChildrenOfName(ReadOnlyMemory<char> name) {
        ArrayBuilder<NamespaceSymbol> namespaceSymbols = null;
        var otherSymbols = ArrayBuilder<Symbol>.GetInstance();

        foreach (var namespaceSymbol in _namespacesToMerge) {
            foreach (var childSymbol in namespaceSymbol.GetMembers(name)) {
                if (childSymbol.kind == SymbolKind.Namespace) {
                    namespaceSymbols ??= ArrayBuilder<NamespaceSymbol>.GetInstance();
                    namespaceSymbols.Add((NamespaceSymbol)childSymbol);
                } else {
                    otherSymbols.Add(childSymbol);
                }
            }
        }

        if (namespaceSymbols is not null)
            otherSymbols.Add(Create(extent, this, namespaceSymbols.ToImmutableAndFree()));

        return otherSymbols.ToImmutableAndFree();
    }

    private SegmentedHashSet<ReadOnlyMemory<char>> SlowGetChildNames(IEqualityComparer<ReadOnlyMemory<char>> comparer) {
        var childCount = 0;

        foreach (var ns in _namespacesToMerge)
            childCount += ns.GetMembersUnordered().Length;

        var childNames = new SegmentedHashSet<ReadOnlyMemory<char>>(childCount, comparer);

        foreach (var ns in _namespacesToMerge) {
            foreach (var child in ns.GetMembersUnordered())
                childNames.Add(child.name.AsMemory());
        }

        return childNames;
    }

    internal override ImmutableArray<Symbol> GetMembers() {
        if (_allMembers.IsDefault) {
            var builder = ArrayBuilder<Symbol>.GetInstance();
            _cachedLookup.AddValues(builder);
            _allMembers = builder.ToImmutableAndFree();
        }

        return _allMembers;
    }

    internal override ImmutableArray<Symbol> GetMembers(ReadOnlyMemory<char> name) {
        return _cachedLookup[name];
    }

    internal sealed override ImmutableArray<NamedTypeSymbol> GetTypeMembersUnordered() {
        return [.. GetMembersUnordered().OfType<NamedTypeSymbol>()];
    }

    internal sealed override ImmutableArray<NamedTypeSymbol> GetTypeMembers() {
        return [.. GetMembers().OfType<NamedTypeSymbol>()];
    }

    internal override ImmutableArray<NamedTypeSymbol> GetTypeMembers(ReadOnlyMemory<char> name) {
        return [.. _cachedLookup[name].OfType<NamedTypeSymbol>()];
    }
}
