using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Class used for handling the creation of SyntaxNodes.
/// </summary>
internal sealed partial class SyntaxFactory {
    private readonly SyntaxTree _syntaxTree;

    internal SyntaxFactory(SyntaxTree syntaxTree) {
        _syntaxTree = syntaxTree;
    }

    internal SyntaxToken Token(SyntaxKind kind, int position, string text) {
        return new SyntaxToken(
            _syntaxTree, kind, position, text, null,
            ImmutableArray<SyntaxTrivia>.Empty, ImmutableArray<SyntaxTrivia>.Empty
        );
    }

    internal SyntaxToken Token(SyntaxKind kind, string text) {
        return Token(kind, -1, text);
    }

    internal SyntaxToken Token(SyntaxKind kind, int position) {
        return Token(kind, position, null);
    }

    internal SyntaxToken Token(SyntaxKind kind) {
        return Token(kind, null);
    }

    internal SyntaxToken Identifier(string name) {
        return new SyntaxToken(
            _syntaxTree, SyntaxKind.IdentifierToken, -1, name, null,
            ImmutableArray<SyntaxTrivia>.Empty, ImmutableArray<SyntaxTrivia>.Empty
        );
    }

    internal NameExpressionSyntax Name(string name) {
        return new NameExpressionSyntax(_syntaxTree, Token(SyntaxKind.IdentifierToken, name));
    }

    internal ReferenceExpressionSyntax Reference(string name) {
        return new ReferenceExpressionSyntax(
            _syntaxTree, Token(SyntaxKind.RefExpression), Token(SyntaxKind.IdentifierToken, name)
        );
    }

    internal SyntaxTrivia Skipped(SyntaxToken badToken) {
        return new SyntaxTrivia(_syntaxTree, SyntaxKind.SkippedTokenTrivia, badToken.position, badToken.text);
    }

    private T Create<T>(T node) where T : SyntaxNode {
        return SyntaxNode.InitializeChildrenParents(node);
    }
}
