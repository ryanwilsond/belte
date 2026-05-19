using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Buckle.CodeAnalysis.Symbols;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Lowering;

internal sealed class SynthesizedEnumMethodContainer : SynthesizedContainer {
    private readonly NamedTypeSymbol _enumType;

    private Dictionary<ReadOnlyMemory<char>, ImmutableArray<Symbol>> _lazyMembersDictionary;
    private ImmutableArray<Symbol> _lazyMembersFlattened;
    private Dictionary<MethodSymbol, MethodSymbol> _lazyMethodMap;

    internal SynthesizedEnumMethodContainer(
        NamedTypeSymbol enumType,
        NamespaceSymbol containingSymbol)
        : base(
            GeneratedNames.MakeEnumMethodContainerName(containingSymbol, enumType.name),
            templateParameters: [],
            templateMap: TemplateMap.Empty) {
        _enumType = enumType;
        this.containingSymbol = containingSymbol;
    }

    internal override Symbol containingSymbol { get; }

    public override TypeKind typeKind => TypeKind.Class;

    internal override bool isStatic => true;

    internal override Accessibility declaredAccessibility => Accessibility.Public;

    internal override NamedTypeSymbol baseType => null;

    internal override IEnumerable<string> memberNames => GetMembers().Select(m => m.name);

    internal Dictionary<MethodSymbol, MethodSymbol> methodMap {
        get {
            if (_lazyMethodMap is null)
                _ = GetMembersByName();

            return _lazyMethodMap;
        }
    }

    internal NamedTypeSymbol enumType => _enumType;

    internal override ImmutableArray<Symbol> GetMembers() {
        if (!_lazyMembersFlattened.IsDefault)
            return _lazyMembersFlattened;

        var members = GetMembersByName().Flatten();

        if (members.Length > 1) {
            members = members.Sort(LexicalOrderSymbolComparer.Instance);
            ImmutableInterlocked.InterlockedExchange(ref _lazyMembersFlattened, members);
        }

        return members;
    }

    internal override ImmutableArray<Symbol> GetMembers(string name) {
        if (GetMembersByName().TryGetValue(name.AsMemory(), out var members))
            return members;

        return [];
    }

    private Dictionary<ReadOnlyMemory<char>, ImmutableArray<Symbol>> GetMembersByName() {
        if (_lazyMembersDictionary is null) {
            var membersDictionary = MakeAllMembers();
            Interlocked.CompareExchange(ref _lazyMembersDictionary, membersDictionary, null);
        }

        return _lazyMembersDictionary;
    }

    private Dictionary<ReadOnlyMemory<char>, ImmutableArray<Symbol>> MakeAllMembers() {
        var builder = ArrayBuilder<Symbol>.GetInstance();
        var methodMap = new Dictionary<MethodSymbol, MethodSymbol>();

        foreach (var member in _enumType.GetMembers()) {
            if (member is not MethodSymbol method || method.methodKind != MethodKind.Ordinary)
                continue;

            var enumMethod = new SynthesizedEnumMethod(this, method);
            methodMap.Add(method, enumMethod);
            builder.Add(enumMethod);
        }

        Interlocked.CompareExchange(ref _lazyMethodMap, methodMap, null);

        return SourceMemberContainerTypeSymbol.ToNameKeyedDictionary(builder.ToImmutableAndFree());
    }
}
