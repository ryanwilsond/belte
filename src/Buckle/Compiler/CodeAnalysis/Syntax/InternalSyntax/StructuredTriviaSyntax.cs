
using Diagnostics;

namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

internal abstract class StructuredTriviaSyntax : BelteSyntaxNode {
    internal StructuredTriviaSyntax(SyntaxKind kind, Diagnostic[] diagnostics = null) : base(kind, diagnostics) {
        _flags |= NodeFlags.ContainsStructuredTrivia;
    }
}
