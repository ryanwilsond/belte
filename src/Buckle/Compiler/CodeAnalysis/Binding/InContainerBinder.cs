using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Binding;

internal class InContainerBinder : Binder {
    internal InContainerBinder(NamespaceOrTypeSymbol container, Binder next) : base(next) {
        this.container = container;
    }

    internal NamespaceOrTypeSymbol container { get; }

    internal override Symbol containingMember => container;

    private protected override SourceLocalSymbol LookupLocal(SyntaxToken identifier) {
        return null;
    }

    private protected override LocalFunctionSymbol LookupLocalFunction(SyntaxToken identifier) {
        return null;
    }
}
