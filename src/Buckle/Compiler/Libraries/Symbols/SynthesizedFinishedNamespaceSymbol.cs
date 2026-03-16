using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Buckle.CodeAnalysis;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.Libraries;

internal sealed class SynthesizedFinishedNamespaceSymbol : NamespaceSymbol {
    private readonly ImmutableArray<Symbol> _allMembers;

    private Dictionary<ReadOnlyMemory<char>, ImmutableArray<Symbol>> _nameToMembersMap;
    private Dictionary<ReadOnlyMemory<char>, ImmutableArray<NamedTypeSymbol>> _nameToTypeMembersMap;

    internal SynthesizedFinishedNamespaceSymbol(NamespaceSymbol underlyingNamespace, ImmutableArray<Symbol> members) {
        name = underlyingNamespace.name;
        _originalSymbolDefinition = underlyingNamespace;
        _allMembers = members;
    }

    public override string name { get; }

    internal override NamespaceExtent extent => new NamespaceExtent();

    internal override Symbol containingSymbol => null;

    internal override SyntaxReference syntaxReference => null;

    internal override TextLocation location => null;

    internal override ImmutableArray<TextLocation> locations => [];

    internal override ImmutableArray<SyntaxReference> declaringSyntaxReferences => [];

    private protected override Symbol _originalSymbolDefinition { get; }

    internal override ImmutableArray<Symbol> GetMembers() {
        return _allMembers;
    }

    internal override ImmutableArray<Symbol> GetMembers(ReadOnlyMemory<char> name) {
        return GetNameToMembersMap().TryGetValue(name, out var members) ? members : [];
    }

    internal override ImmutableArray<NamedTypeSymbol> GetTypeMembers() {
        return GetNameToTypeMembersMap().Flatten(LexicalOrderSymbolComparer.Instance);
    }

    internal override ImmutableArray<NamedTypeSymbol> GetTypeMembers(ReadOnlyMemory<char> name) {
        return GetNameToTypeMembersMap().TryGetValue(name, out var members) ? members : [];
    }

    private Dictionary<ReadOnlyMemory<char>, ImmutableArray<Symbol>> GetNameToMembersMap() {
        if (_nameToMembersMap is null)
            Interlocked.CompareExchange(ref _nameToMembersMap, MakeNameToMembersMap(), null);

        return _nameToMembersMap;
    }

    private Dictionary<ReadOnlyMemory<char>, ImmutableArray<NamedTypeSymbol>> GetNameToTypeMembersMap() {
        if (_nameToTypeMembersMap is null) {
            Interlocked.CompareExchange(
                ref _nameToTypeMembersMap,
                ImmutableArrayExtensions
                    .GetTypesFromMemberMap<ReadOnlyMemory<char>, Symbol, NamedTypeSymbol>(
                        GetNameToMembersMap(),
                        ReadOnlyMemoryOfCharComparer.Instance
                    ),
                null
            );
        }

        return _nameToTypeMembersMap;
    }

    private Dictionary<ReadOnlyMemory<char>, ImmutableArray<Symbol>> MakeNameToMembersMap() {
        var builder = NameToObjectPool.Allocate();

        foreach (var symbol in _allMembers) {
            ImmutableArrayExtensions.AddToMultiValueDictionaryBuilder(
                builder,
                symbol.name.AsMemory(),
                symbol
            );
        }

        var result = new Dictionary<ReadOnlyMemory<char>, ImmutableArray<Symbol>>(
            builder.Count,
            ReadOnlyMemoryOfCharComparer.Instance
        );

        foreach (var pair in builder) {
            result.Add(pair.Key, pair.Value is ArrayBuilder<Symbol> arrayBuilder
                ? arrayBuilder.ToImmutableAndFree()
                : [(Symbol)pair.Value]);
        }

        builder.Free();
        return result;
    }
}
