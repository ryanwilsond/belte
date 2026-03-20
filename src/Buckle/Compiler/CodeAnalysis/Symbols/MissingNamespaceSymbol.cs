using System;
using System.Collections.Immutable;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Symbols;

internal class MissingNamespaceSymbol : NamespaceSymbol {
    private readonly Symbol _containingSymbol;

    public MissingNamespaceSymbol(MissingModuleSymbol containingModule) {
        _containingSymbol = containingModule;
        name = "";
    }

    public MissingNamespaceSymbol(NamespaceSymbol containingNamespace, string name) {
        _containingSymbol = containingNamespace;
        this.name = name;
    }

    public override string name { get; }

    internal override Symbol containingSymbol => _containingSymbol;

    internal override AssemblySymbol containingAssembly => _containingSymbol.containingAssembly;

    internal override NamespaceExtent extent {
        get {
            // TODO Switched to Assembly extent over Module for better interop
            if (_containingSymbol.kind == SymbolKind.Assembly)
                return new NamespaceExtent(containingAssembly);

            return ((NamespaceSymbol)_containingSymbol).extent;
        }
    }

    internal override ImmutableArray<TextLocation> locations => [];

    internal override ImmutableArray<SyntaxReference> declaringSyntaxReferences => [];

    internal override SyntaxReference syntaxReference => null;

    internal override TextLocation location => null;

    internal override ImmutableArray<NamedTypeSymbol> GetTypeMembers() {
        return [];
    }

    internal override ImmutableArray<NamedTypeSymbol> GetTypeMembers(ReadOnlyMemory<char> name) {
        return [];
    }

    internal override ImmutableArray<NamedTypeSymbol> GetTypeMembers(ReadOnlyMemory<char> name, int arity) {
        return [];
    }

    internal override ImmutableArray<Symbol> GetMembers() {
        return [];
    }

    internal override ImmutableArray<Symbol> GetMembers(ReadOnlyMemory<char> name) {
        return [];
    }

    public override int GetHashCode() {
        return Hash.Combine(_containingSymbol.GetHashCode(), name.GetHashCode());
    }

    internal override bool Equals(Symbol obj, TypeCompareKind compareKind) {
        if (ReferenceEquals(this, obj))
            return true;

        return obj is MissingNamespaceSymbol other &&
            name.Equals(other.name) &&
            _containingSymbol.Equals(other._containingSymbol, compareKind);
    }
}
