using System;
using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;

namespace Buckle.Libraries;

internal sealed class SynthesizedSimpleNamespaceSymbol : NamespaceSymbol {

    internal SynthesizedSimpleNamespaceSymbol(string name) {
        this.name = name;
    }

    public override string name { get; }

    internal override NamespaceExtent extent => new NamespaceExtent();

    internal override Symbol containingSymbol => null;

    internal override SyntaxReference syntaxReference => null;

    internal override TextLocation location => null;

    internal override ImmutableArray<TextLocation> locations => [];

    internal override ImmutableArray<SyntaxReference> declaringSyntaxReferences => [];

    internal override ImmutableArray<Symbol> GetMembers() {
        throw new InvalidOperationException();
    }

    internal override ImmutableArray<Symbol> GetMembers(ReadOnlyMemory<char> name) {
        throw new InvalidOperationException();
    }

    internal override ImmutableArray<NamedTypeSymbol> GetTypeMembers() {
        throw new InvalidOperationException();
    }

    internal override ImmutableArray<NamedTypeSymbol> GetTypeMembers(ReadOnlyMemory<char> name) {
        throw new InvalidOperationException();
    }
}
