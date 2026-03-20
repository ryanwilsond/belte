using System.Collections.Immutable;

namespace Buckle.CodeAnalysis;

internal abstract class Declaration {
    private protected Declaration(string name) {
        this.name = name;
    }

    internal string name { get; }

    internal ImmutableArray<Declaration> children => GetDeclarationChildren();

    internal abstract DeclarationKind kind { get; }

    private protected abstract ImmutableArray<Declaration> GetDeclarationChildren();
}
