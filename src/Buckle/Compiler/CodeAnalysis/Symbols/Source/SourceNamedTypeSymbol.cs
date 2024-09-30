using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class SourceNamedTypeSymbol : SourceMemberContainerTypeSymbol {
    private readonly TemplateParameterInfo _templateParameterInfo;

    internal SourceNamedTypeSymbol(
        NamespaceOrTypeSymbol containingSymbol,
        TypeDeclarationSyntax declaration,
        BelteDiagnosticQueue diagnostics,
        Compilation declaringCompilation)
        : base(containingSymbol, declaration, diagnostics, declaringCompilation) {
        _templateParameterInfo = arity == 0 ? TemplateParameterInfo.Empty : new TemplateParameterInfo();
    }
}
