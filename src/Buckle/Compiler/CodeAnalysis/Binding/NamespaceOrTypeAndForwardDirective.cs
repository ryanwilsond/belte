using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Binding;

internal readonly struct NamespaceOrTypeAndForwardDirective {
    internal readonly NamespaceOrTypeSymbol namespaceOrType;
    internal readonly SyntaxReference forwardDirectiveReference;
    internal readonly ImmutableArray<AssemblySymbol> dependencies;
    internal readonly string unresolvedTarget;
    internal readonly bool isUnresolvedType;

    internal NamespaceOrTypeAndForwardDirective(
        NamespaceOrTypeSymbol namespaceOrType,
        ForwardDirectiveSyntax forwardDirective,
        ImmutableArray<AssemblySymbol> dependencies) {
        this.namespaceOrType = namespaceOrType;
        forwardDirectiveReference = new SyntaxReference(forwardDirective);
        this.dependencies = dependencies.NullToEmpty();
    }

    internal NamespaceOrTypeAndForwardDirective(string unresolvedTarget, bool isType) {
        this.unresolvedTarget = unresolvedTarget;
        isUnresolvedType = isType;
        dependencies = [];
    }

    internal ForwardDirectiveSyntax forwardDirective => forwardDirectiveReference?.node as ForwardDirectiveSyntax;
}
