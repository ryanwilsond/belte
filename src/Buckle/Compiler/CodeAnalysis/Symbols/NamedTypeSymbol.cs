using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class NamedTypeSymbol : TypeSymbol, ITypeSymbolWithMembers {
    private Dictionary<string, ImmutableArray<Symbol>> _lazyMembersDictionary;

    internal NamedTypeSymbol(
        ImmutableArray<ParameterSymbol> templateParameters,
        ImmutableArray<Symbol> symbols,
        TypeDeclarationSyntax declaration)
        : base(declaration.identifier.text) {
        this.members = symbols;
        this.declaration = declaration;
        this.templateParameters = templateParameters;

        foreach (var member in members)
            member.SetContainingType(this);
    }

    public override SymbolKind kind => SymbolKind.Type;

    public ImmutableArray<Symbol> members { get; }

    public ImmutableArray<MethodSymbol> constructors => GetConstructors();

    internal ImmutableArray<ParameterSymbol> templateParameters { get; }

    internal override int arity => templateParameters.Length;

    internal TypeDeclarationSyntax declaration { get; }

    public ImmutableArray<Symbol> GetMembers(string name) {
        if (_lazyMembersDictionary is null)
            ConstructLazyMembersDictionary();

        return _lazyMembersDictionary[name];
    }

    /// <summary>
    /// Gets a string representation of the type signature without template parameter names.
    /// </summary>
    internal string Signature() {
        var signature = new StringBuilder($"{name}<");
        var isFirst = true;

        foreach (var parameter in templateParameters) {
            if (isFirst)
                isFirst = false;
            else
                signature.Append(", ");

            signature.Append(parameter.type);
        }

        signature.Append('>');

        return signature.ToString();
    }

    private ImmutableArray<MethodSymbol> GetConstructors() {
        var candidates = GetMembers(WellKnownMemberNames.InstanceConstructorName);

        if (candidates.IsEmpty)
            return ImmutableArray<MethodSymbol>.Empty;

        ArrayBuilder<MethodSymbol> constructors = ArrayBuilder<MethodSymbol>.GetInstance();

        foreach (var candidate in candidates) {
            if (candidate is MethodSymbol method)
                constructors.Add(method);
        }

        return constructors.ToImmutableAndFree();
    }

    private void ConstructLazyMembersDictionary() {
        _lazyMembersDictionary = members.ToDictionary(m => m.name, StringOrdinalComparer.Instance);
    }
}
