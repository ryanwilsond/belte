using System;
using System.Collections.Immutable;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class NamespaceSymbol : NamespaceOrTypeSymbol, INamespaceSymbol {
    public sealed override SymbolKind kind => SymbolKind.Namespace;

    public virtual bool isGlobalNamespace => containingNamespace is null;

    public NamespaceKind namespaceKind => extent.kind;

    public Compilation containingCompilation
        => namespaceKind == NamespaceKind.Compilation ? extent.compilation : null;

    internal sealed override bool isImplicitlyDeclared => isGlobalNamespace;

    internal sealed override NamedTypeSymbol containingType => null;

    internal sealed override bool isStatic => true;

    internal sealed override bool isAbstract => false;

    internal sealed override bool isSealed => false;

    internal sealed override Accessibility declaredAccessibility => Accessibility.Public;

    internal abstract NamespaceExtent extent { get; }

    internal abstract override ImmutableArray<SyntaxReference> declaringSyntaxReferences { get; }

    internal abstract override ImmutableArray<TextLocation> locations { get; }

    internal NamedTypeSymbol implicitType {
        get {
            var types = GetTypeMembers(TypeSymbol.ImplicitTypeName);

            if (types.Length == 0)
                return null;

            return types[0];
        }
    }

    internal virtual ImmutableArray<NamespaceSymbol> constituentNamespaces => [this];

    internal abstract ImmutableArray<Symbol> GetMembers(ReadOnlyMemory<char> name);

    internal sealed override ImmutableArray<Symbol> GetMembers(string name)
        => GetMembers(name.AsMemory());

    internal NamespaceSymbol GetNestedNamespace(string name)
        => GetNestedNamespace(name.AsMemory());

    internal virtual NamespaceSymbol GetNestedNamespace(ReadOnlyMemory<char> name) {
        foreach (var sym in GetMembers(name)) {
            if (sym.kind == SymbolKind.Namespace)
                return (NamespaceSymbol)sym;
        }

        return null;
    }

    internal NamespaceSymbol GetNestedNamespace(NameSyntax name) {
        switch (name.kind) {
            case SyntaxKind.TemplateName:
            case SyntaxKind.IdentifierName:
                return GetNestedNamespace(((SimpleNameSyntax)name).identifier.text);
            case SyntaxKind.QualifiedName:
                var qn = (QualifiedNameSyntax)name;
                var leftNs = GetNestedNamespace(qn.left);

                if (leftNs is not null)
                    return leftNs.GetNestedNamespace(qn.right);

                break;
            case SyntaxKind.AliasQualifiedName:
                return GetNestedNamespace(name.GetUnqualifiedName().identifier.text);
        }

        return null;
    }

    internal NamespaceSymbol LookupNestedNamespace(ImmutableArray<ReadOnlyMemory<char>> names) {
        var scope = this;

        foreach (var name in names) {
            scope = scope.GetNestedNamespace(name);

            if (scope is null)
                return null;
        }

        return scope;
    }

    internal override void Accept(SymbolVisitor visitor) {
        visitor.VisitNamespace(this);
    }

    internal override TResult Accept<TArgument, TResult>(
        SymbolVisitor<TArgument, TResult> visitor,
        TArgument argument) {
        return visitor.VisitNamespace(this, argument);
    }
}
