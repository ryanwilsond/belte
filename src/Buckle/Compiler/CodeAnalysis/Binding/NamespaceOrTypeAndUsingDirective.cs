using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Binding;

internal readonly struct NamespaceOrTypeAndUsingDirective {
    internal readonly NamespaceOrTypeSymbol namespaceOrType;
    internal readonly SyntaxReference usingDirectiveReference;
    internal readonly ImmutableArray<AssemblySymbol> dependencies;

    internal NamespaceOrTypeAndUsingDirective(
        NamespaceOrTypeSymbol namespaceOrType,
        UsingDirectiveSyntax usingDirective,
        ImmutableArray<AssemblySymbol> dependencies) {
        this.namespaceOrType = namespaceOrType;
        usingDirectiveReference = new SyntaxReference(usingDirective);
        this.dependencies = dependencies.NullToEmpty();
    }

    internal UsingDirectiveSyntax usingDirective => (UsingDirectiveSyntax)usingDirectiveReference?.node;
}
