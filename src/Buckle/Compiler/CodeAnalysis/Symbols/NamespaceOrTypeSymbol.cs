using System;
using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class NamespaceOrTypeSymbol : Symbol {
    internal bool isNamespace => kind == SymbolKind.Namespace;

    internal bool isType => !isNamespace;

    internal sealed override bool isOverride => false;

    internal sealed override bool isVirtual => false;

    internal abstract ImmutableArray<Symbol> GetMembers();

    internal abstract ImmutableArray<Symbol> GetMembers(string name);

    internal abstract ImmutableArray<NamedTypeSymbol> GetTypeMembers();

    internal ImmutableArray<NamedTypeSymbol> GetTypeMembers(string name)
        => GetTypeMembers(name.AsMemory());

    internal abstract ImmutableArray<NamedTypeSymbol> GetTypeMembers(ReadOnlyMemory<char> name);
}
