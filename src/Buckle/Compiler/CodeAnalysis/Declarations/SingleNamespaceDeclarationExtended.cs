using System.Collections.Immutable;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis;

internal sealed class SingleNamespaceDeclarationExtended : SingleNamespaceDeclaration {
    internal SingleNamespaceDeclarationExtended(
        string name,
        bool hasUsings,
        bool hasExternAliases,
        SyntaxReference syntaxReference,
        TextLocation nameLocation,
        ImmutableArray<SingleNamespaceOrTypeDeclaration> children,
        ImmutableArray<BelteDiagnostic> diagnostics)
        : base(name, syntaxReference, nameLocation, children, diagnostics) {
        this.hasUsings = hasUsings;
        this.hasExternAliases = hasExternAliases;
    }

    internal override bool hasUsings { get; }

    internal override bool hasExternAliases { get; }
}
