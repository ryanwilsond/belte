using System;
using System.Collections.Immutable;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class GlobalNamespaceSymbol : NamespaceSymbol {
    internal GlobalNamespaceSymbol(NamespaceExtent extent) {
        this.extent = extent;
    }

    internal override NamespaceExtent extent { get; }

    internal override SyntaxReference syntaxReference => null;

    internal override Symbol containingSymbol => null;

    internal override ImmutableArray<Symbol> GetMembers() {
        return [];
    }

    internal override ImmutableArray<Symbol> GetMembers(ReadOnlyMemory<char> name) {
        return [];
    }

    internal override ImmutableArray<NamedTypeSymbol> GetTypeMembers() {
        return [];
    }

    internal override ImmutableArray<NamedTypeSymbol> GetTypeMembers(ReadOnlyMemory<char> name) {
        return [];
    }
}
