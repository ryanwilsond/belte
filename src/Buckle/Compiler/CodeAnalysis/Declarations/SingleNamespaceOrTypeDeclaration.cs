using System.Collections.Immutable;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis;

internal abstract class SingleNamespaceOrTypeDeclaration : Declaration {
    internal readonly ImmutableArray<BelteDiagnostic> diagnostics;

    private protected SingleNamespaceOrTypeDeclaration(
        string name,
        SyntaxReference syntaxReference,
        TextLocation nameLocation,
        ImmutableArray<BelteDiagnostic> diagnostics)
        : base(name) {
        this.syntaxReference = syntaxReference;
        this.nameLocation = nameLocation;
        this.diagnostics = diagnostics;
    }

    internal TextLocation location => syntaxReference.location;

    internal SyntaxReference syntaxReference { get; }

    internal TextLocation nameLocation { get; }

    internal new ImmutableArray<SingleNamespaceOrTypeDeclaration> children => GetNamespaceOrTypeDeclarationChildren();

    private protected override ImmutableArray<Declaration> GetDeclarationChildren() {
        return StaticCast<Declaration>.From(GetNamespaceOrTypeDeclarationChildren());
    }

    private protected abstract ImmutableArray<SingleNamespaceOrTypeDeclaration> GetNamespaceOrTypeDeclarationChildren();
}
