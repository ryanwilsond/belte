using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Buckle.CodeAnalysis;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.Libraries;

internal sealed class SynthesizedFinishedNamedTypeSymbol : WrappedNamedTypeSymbol {
    private readonly ImmutableArray<Symbol> _allMembers;

    private Dictionary<ReadOnlyMemory<char>, ImmutableArray<Symbol>> _nameToMembersMap;
    private Dictionary<ReadOnlyMemory<char>, ImmutableArray<NamedTypeSymbol>> _nameToTypeMembersMap;

    internal SynthesizedFinishedNamedTypeSymbol(
        NamedTypeSymbol underlyingType,
        Symbol containingSymbol,
        ImmutableArray<Symbol>? members = null,
        bool isSimpleProgram = false)
        : base(underlyingType) {
        this.containingSymbol = containingSymbol;
        _allMembers = members ?? underlyingType.GetMembers();
        this.isSimpleProgram = isSimpleProgram;
    }

    public override ImmutableArray<TemplateParameterSymbol> templateParameters => [];

    public override ImmutableArray<BoundExpression> templateConstraints => [];

    public override ImmutableArray<TypeOrConstant> templateArguments => [];

    internal override IEnumerable<string> memberNames => GetMembers().Select(m => m.name);

    internal override NamedTypeSymbol constructedFrom => this;

    internal override NamedTypeSymbol baseType => underlyingNamedType.baseType;

    internal override Symbol containingSymbol { get; }

    internal override bool isSimpleProgram { get; }

    internal override LexicalSortKey GetLexicalSortKey() {
        return LexicalSortKey.NotInSource;
    }

    internal override NamedTypeSymbol GetDeclaredBaseType(ConsList<TypeSymbol> basesBeingResolved) {
        return baseType;
    }

    internal override ImmutableArray<Symbol> GetMembers() {
        return _allMembers;
    }

    internal override ImmutableArray<Symbol> GetMembers(string name) {
        return GetNameToMembersMap().TryGetValue(name.AsMemory(), out var members) ? members : [];
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
                CodeAnalysis.ImmutableArrayExtensions
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
            CodeAnalysis.ImmutableArrayExtensions.AddToMultiValueDictionaryBuilder(
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
