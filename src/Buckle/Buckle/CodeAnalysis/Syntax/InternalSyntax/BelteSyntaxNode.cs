using Diagnostics;

namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

internal abstract class BelteSyntaxNode : GreenNode {
    internal BelteSyntaxNode(SyntaxKind kind) : base(kind) { }

    internal BelteSyntaxNode(SyntaxKind kind, int fullWidth) : base(kind, fullWidth) { }

    internal BelteSyntaxNode(SyntaxKind kind, Diagnostic[] diagnostics) : base(kind, diagnostics) { }

    internal BelteSyntaxNode(SyntaxKind kind, int fullWidth, Diagnostic[] diagnostics)
        : base(kind, fullWidth, diagnostics) { }

    internal SyntaxToken GetFirstToken() {
        return (SyntaxToken)GetFirstTerminal();
    }

    internal SyntaxToken GetLastToken() {
        return (SyntaxToken)GetLastTerminal();
    }

    internal abstract TResult Accept<TResult>(SyntaxVisitor<TResult> visitor);

    internal abstract void Accept(SyntaxVisitor visitor);
}
