using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Represents a token in the syntax tree.
/// </summary>
public sealed class SyntaxToken {
    internal SyntaxToken(SyntaxNode parent, GreenNode token, int position, int index) {
        this.parent = parent;
        node = token;
        this.position = position;
        this.index = index;
    }

    internal SyntaxNode parent { get; }

    internal GreenNode node { get; }

    internal int position { get; }

    internal int index { get; }

    internal SyntaxKind kind => node.kind;

    internal object value => node.GetValue();

    internal int width => node?.width ?? 0;

    internal int fullWidth => node?.fullWidth ?? 0;

    internal TextSpan span => node != null ? new TextSpan(position + node.GetLeadingTriviaWidth(), node.width) : null;

    internal SyntaxTree syntaxTree => parent?.syntaxTree;

    internal TextLocation location => syntaxTree != null ? new TextLocation(syntaxTree.text, span) : null;

    internal SyntaxTriviaList leadingTrivia => node != null ?
        new SyntaxTriviaList(this, node.GetLeadingTrivia(), position)
        : null;

    internal SyntaxTriviaList trailingTrivia {
        get {
            if (node == null)
                return null;

            var leading = node.GetLeadingTrivia();
            int index = 0;

            if (leading != null)
                index = 1;

            var trailingGreen = node.GetTrailingTrivia();
            int trailingPosition = position + fullWidth;

            if (trailingGreen != null)
                trailingPosition -= trailingGreen.fullWidth;

            return new SyntaxTriviaList(this, trailingGreen, trailingPosition, index);
        }
    }
}
