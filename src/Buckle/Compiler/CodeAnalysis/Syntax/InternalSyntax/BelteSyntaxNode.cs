using Diagnostics;

namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

/// <summary>
/// Represents a <see cref="GreenNode" /> that is part of the language syntax.
/// </summary>
internal abstract class BelteSyntaxNode : GreenNode {
    /// <summary>
    /// Creates a new <see cref="BelteSyntaxNode" />.
    /// </summary>
    internal BelteSyntaxNode(SyntaxKind kind) : base(kind) { }

    /// <summary>
    /// Creates a new <see cref="BelteSyntaxNode" /> with a predefined full width.
    /// </summary>
    internal BelteSyntaxNode(SyntaxKind kind, int fullWidth) : base(kind, fullWidth) { }

    /// <summary>
    /// Creates a new <see cref="BelteSyntaxNode" /> with diagnostics.
    /// </summary>
    internal BelteSyntaxNode(SyntaxKind kind, Diagnostic[] diagnostics) : base(kind, diagnostics) { }

    /// <summary>
    /// Creates a new <see cref="BelteSyntaxNode" /> with a predefined full width and diagnostics.
    /// </summary>
    internal BelteSyntaxNode(SyntaxKind kind, int fullWidth, Diagnostic[] diagnostics)
        : base(kind, fullWidth, diagnostics) { }

    /// <summary>
    /// Gets the first token this <see cref="BelteSyntaxNode" /> contains.
    /// </summary>
    internal SyntaxToken GetFirstToken() {
        return (SyntaxToken)GetFirstTerminal();
    }

    /// <summary>
    /// Gets the last token this <see cref="BelteSyntaxNode" /> contains.
    /// </summary>
    internal SyntaxToken GetLastToken() {
        return (SyntaxToken)GetLastTerminal();
    }

    /// <summary>
    /// Accepts a <see cref="SyntaxVisitor<TResult>" />.
    /// </summary>
    internal abstract TResult Accept<TResult>(SyntaxVisitor<TResult> visitor);

    /// <summary>
    /// Accepts a <see cref="SyntaxVisitor" />.
    /// </summary>
    internal abstract void Accept(SyntaxVisitor visitor);

    internal virtual DirectiveStack ApplyDirectives(DirectiveStack stack) {
        return ApplyDirectives(this, stack);
    }

    internal static DirectiveStack ApplyDirectives(GreenNode node, DirectiveStack stack) {
        if (node.containsDirectives) {
            for (int i = 0, n = node.slotCount; i < n; i++) {
                var child = node.GetSlot(i);

                if (child is not null)
                    stack = ApplyDirectivesToListOrNode(child, stack);
            }
        }

        return stack;
    }

    internal static DirectiveStack ApplyDirectivesToListOrNode(GreenNode listOrNode, DirectiveStack stack) {
        if (listOrNode.kind == ListKind)
            return ApplyDirectives(listOrNode, stack);

        return ((BelteSyntaxNode)listOrNode).ApplyDirectives(stack);
    }
}
