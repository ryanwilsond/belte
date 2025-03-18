using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class SynthesizedProgram : NamedTypeSymbol {
    private readonly DeclarationModifiers _modifiers;

    private ImmutableArray<Symbol> _allMembers;
    private Dictionary<ReadOnlyMemory<char>, ImmutableArray<Symbol>> _nameToMembersMap;
    private Dictionary<ReadOnlyMemory<char>, ImmutableArray<NamedTypeSymbol>> _nameToTypeMembersMap;
    private bool _allMembersIsSorted;

    internal SynthesizedProgram(
        Symbol container,
        string name,
        TypeKind typeKind,
        NamedTypeSymbol baseType,
        DeclarationModifiers modifiers) {
        containingSymbol = container;
        this.name = name;
        this.typeKind = typeKind;
        this.baseType = baseType;
        _modifiers = modifiers;
    }

    public override string name { get; }

    public override ImmutableArray<TemplateParameterSymbol> templateParameters => [];

    public override ImmutableArray<BoundExpression> templateConstraints => [];

    public override ImmutableArray<TypeOrConstant> templateArguments => [];

    public override int arity => 0;

    public override TypeKind typeKind { get; }

    internal override bool mangleName => false;

    internal override Accessibility declaredAccessibility => ModifierHelpers.EffectiveAccessibility(_modifiers);

    internal override NamedTypeSymbol baseType { get; }

    internal override Symbol containingSymbol { get; }

    internal override bool isStatic => (_modifiers & DeclarationModifiers.Static) != 0;

    internal override bool isAbstract => (_modifiers & DeclarationModifiers.Abstract) != 0;

    internal override bool isSealed => (_modifiers & DeclarationModifiers.Sealed) != 0;

    internal override SyntaxReference syntaxReference => null;

    internal override TextLocation location => null;

    internal override IEnumerable<string> memberNames => GetMembers().Select(m => m.name);

    internal override NamedTypeSymbol constructedFrom => this;

    internal override bool isSimpleProgram => true;

    internal override LexicalSortKey GetLexicalSortKey() {
        return LexicalSortKey.GetSynthesizedMemberKey(0);
    }

    internal void FinishProgram(ImmutableArray<Symbol> members) {
        _allMembers = members;
    }

    internal override NamedTypeSymbol GetDeclaredBaseType(ConsList<TypeSymbol> basesBeingResolved) {
        return baseType;
    }

    internal override ImmutableArray<Symbol> GetMembers() {
        if (_allMembersIsSorted)
            return _allMembers;

        var allMembers = _allMembers;

        if (allMembers.Length > 1) {
            allMembers = allMembers.Sort(LexicalOrderSymbolComparer.Instance);
            ImmutableInterlocked.InterlockedExchange(ref _allMembers, allMembers);
        }

        _allMembersIsSorted = true;
        return allMembers;
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
