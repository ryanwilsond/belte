using System.Collections.Immutable;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis;

internal sealed class RootSingleNamespaceDeclaration : SingleNamespaceDeclaration {
    internal RootSingleNamespaceDeclaration(
        bool hasGlobalUsings,
        bool hasUsings,
        bool hasExternAliases,
        SyntaxReference treeNode,
        ImmutableArray<SingleNamespaceOrTypeDeclaration> children,
        // ImmutableArray<ReferenceDirective> referenceDirectives,
        bool hasAssemblyAttributes,
        ImmutableArray<BelteDiagnostic> diagnostics)
        : base(
            "<global>",
            treeNode,
            nameLocation: treeNode.location,
            children: children,
            diagnostics: diagnostics) {
        // this.referenceDirectives = referenceDirectives;
        this.hasGlobalUsings = hasGlobalUsings;
        this.hasUsings = hasUsings;
        this.hasExternAliases = hasExternAliases;
        this.hasAssemblyAttributes = hasAssemblyAttributes;
    }

    // internal ImmutableArray<ReferenceDirective> referenceDirectives { get; }

    internal override bool hasGlobalUsings { get; }

    internal override bool hasUsings { get; }

    internal override bool hasExternAliases { get; }

    internal override bool hasAssemblyAttributes { get; }
}
