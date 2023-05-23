using System.Collections.Generic;
using System.Collections.Immutable;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class NamedTypeSymbol : TypeSymbol, ITypeSymbolWithMembers {
    private Dictionary<string, ImmutableArray<Symbol>> _lazyMembersDictionary;

    internal NamedTypeSymbol(
        ImmutableArray<TemplateParameterSymbol> templateParameters,
        ImmutableArray<Symbol> symbols,
        TypeDeclarationSyntax declaration)
        : base(declaration.identifier.text) {
        this.members = symbols;
        this.declaration = declaration;
        this.templateParameters = templateParameters;
    }

    public override SymbolKind kind => SymbolKind.Type;

    public ImmutableArray<Symbol> members { get; }

    internal ImmutableArray<TemplateParameterSymbol> templateParameters { get; }

    internal override int arity => templateParameters.Length;

    internal TypeDeclarationSyntax declaration { get; }

    public ImmutableArray<Symbol> GetMembers() {
        return members;
    }

    public ImmutableArray<Symbol> GetMembers(string name) {
        if (_lazyMembersDictionary == null)
            ConstructLazyMembersDictionary();

        return _lazyMembersDictionary[name];
    }

    private void ConstructLazyMembersDictionary() {
        _lazyMembersDictionary = members.ToDictionary(m => m.name, StringOrdinalComparer.Instance);
    }
}
