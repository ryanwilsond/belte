using System;
using Buckle.Diagnostics;
using Buckle.Libraries;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class GlobalNamespaceSymbol : SourceNamespaceSymbol {
    internal GlobalNamespaceSymbol(
        NamespaceExtent extent,
        MergedNamespaceDeclaration mergedDeclaration,
        BelteDiagnosticQueue diagnostics)
        : base(null, mergedDeclaration, diagnostics) {
        this.extent = extent;
    }

    internal override NamespaceExtent extent { get; }

    internal override Symbol containingSymbol => null;

    private protected override void AdditionalRegistration(
        PooledDictionary<ReadOnlyMemory<char>, object> builder,
        CompilationOptions options) {
        LibraryHelpers.DeclareLibrariesInNamespace(builder, declaringCompilation.options);
    }
}
