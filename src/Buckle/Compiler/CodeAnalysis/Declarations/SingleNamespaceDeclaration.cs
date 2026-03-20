using System.Collections.Immutable;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis;

internal class SingleNamespaceDeclaration : SingleNamespaceOrTypeDeclaration {
    private readonly ImmutableArray<SingleNamespaceOrTypeDeclaration> _children;

    protected SingleNamespaceDeclaration(
        string name,
        SyntaxReference syntaxReference,
        TextLocation nameLocation,
        ImmutableArray<SingleNamespaceOrTypeDeclaration> children,
        ImmutableArray<BelteDiagnostic> diagnostics)
        : base(name, syntaxReference, nameLocation, diagnostics) {
        _children = children;
    }

    internal override DeclarationKind kind => DeclarationKind.Namespace;

    internal virtual bool hasGlobalUsings => false;

    internal virtual bool hasUsings => false;

    internal virtual bool hasExternAliases => false;

    internal virtual bool hasAssemblyAttributes => false;

    internal static SingleNamespaceDeclaration Create(
        string name,
        bool hasUsings,
        bool hasExternAliases,
        SyntaxReference syntaxReference,
        TextLocation nameLocation,
        ImmutableArray<SingleNamespaceOrTypeDeclaration> children,
        ImmutableArray<BelteDiagnostic> diagnostics) {
        // Optimize for the most common "no usings, no extern aliases" case
        if (!hasUsings && !hasExternAliases) {
            return new SingleNamespaceDeclaration(
                name, syntaxReference, nameLocation, children, diagnostics);
        } else {
            return new SingleNamespaceDeclarationExtended(
                name, hasUsings, hasExternAliases, syntaxReference, nameLocation, children, diagnostics);
        }
    }

    private protected override ImmutableArray<SingleNamespaceOrTypeDeclaration>
        GetNamespaceOrTypeDeclarationChildren() {
        return _children;
    }
}
