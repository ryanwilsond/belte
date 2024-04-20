using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class NamedTypeSymbol : TypeSymbol, ITypeSymbolWithMembers {
    private readonly DeclarationModifiers _declarationModifiers;
    private Dictionary<string, ImmutableArray<Symbol>> _lazyMembersDictionary;

    internal NamedTypeSymbol(
        ImmutableArray<ParameterSymbol> templateParameters,
        ImmutableArray<Symbol> symbols,
        TypeDeclarationSyntax declaration,
        DeclarationModifiers modifiers)
        : base(declaration?.identifier?.text) {
        members = symbols;
        this.declaration = declaration;
        this.templateParameters = templateParameters;
        _declarationModifiers = modifiers;

        foreach (var member in members)
            member.SetContainingType(this);
    }

    public override SymbolKind kind => SymbolKind.Type;

    public override bool isStatic => (_declarationModifiers & DeclarationModifiers.Static) != 0;

    public ImmutableArray<MethodSymbol> constructors => GetConstructors();

    internal ImmutableArray<Symbol> members { get; private set; }

    internal ImmutableArray<ParameterSymbol> templateParameters { get; private set; }

    internal override int arity => templateParameters.Length;

    internal TypeDeclarationSyntax declaration { get; }

    public ImmutableArray<Symbol> GetMembers(string name) {
        if (_lazyMembersDictionary is null)
            ConstructLazyMembersDictionary();

        return _lazyMembersDictionary[name];
    }

    public ImmutableArray<ISymbol> GetMembers() {
        return members.CastArray<ISymbol>();
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

    internal void UpdateInternals(
        ImmutableArray<ParameterSymbol> templateParameters,
        ImmutableArray<Symbol> symbols) {
        this.templateParameters = templateParameters;
        members = symbols;
    }

    private ImmutableArray<MethodSymbol> GetConstructors() {
        var candidates = GetMembers(WellKnownMemberNames.InstanceConstructorName);

        if (candidates.IsEmpty)
            return ImmutableArray<MethodSymbol>.Empty;

        var constructors = ArrayBuilder<MethodSymbol>.GetInstance();

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
