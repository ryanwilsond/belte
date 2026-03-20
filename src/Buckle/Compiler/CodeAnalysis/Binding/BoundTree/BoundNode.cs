using System;
using System.Diagnostics;
using Buckle.CodeAnalysis.Display;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// Bound node, gets created from a <see cref="SyntaxNode" />.
/// </summary>
[DebuggerDisplay("{GetDebuggerDisplay(), nq}")]
internal abstract class BoundNode {
    private protected BoundNode(BoundKind kind, SyntaxNode syntax, bool hasErrors) {
        this.kind = kind;
        this.hasErrors = hasErrors;
        this.syntax = syntax;
    }

    private protected BoundNode(BoundKind kind, SyntaxNode syntax) {
        this.kind = kind;
        this.syntax = syntax;
    }

    internal BoundKind kind { get; }

    internal bool hasErrors { get; }

    internal SyntaxNode syntax { get; }

    internal virtual BoundNode Accept(BoundTreeVisitor visitor) {
        throw new NotImplementedException();
    }

    public override string ToString() {
        return DisplayText.DisplayNode(this).ToString();
    }

    private string GetDebuggerDisplay() {
        return GetType().Name + " " + ToString();
    }
}
