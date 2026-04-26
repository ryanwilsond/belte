using Diagnostics;

namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

internal abstract class StructuredTriviaSyntax : BelteSyntaxNode {
    internal sealed override bool isStructuredTrivia => true;

    internal StructuredTriviaSyntax(SyntaxKind kind, Diagnostic[] diagnostics = null) : base(kind, diagnostics) {
        _flags |= NodeFlags.ContainsStructuredTrivia;
    }
}
