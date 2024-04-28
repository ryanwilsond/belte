
namespace Buckle.CodeAnalysis.Syntax;

public abstract class StructuredTriviaSyntax : BelteSyntaxNode {
    private SyntaxTrivia _parent;

    internal StructuredTriviaSyntax(SyntaxNode parent, GreenNode green, int position)
        : base(green, position, parent?.syntaxTree) { }

    internal static StructuredTriviaSyntax Create(SyntaxTrivia trivia) {
        var node = trivia.green;
        var parent = trivia.token.parent;
        var position = trivia.position;
        var red = (StructuredTriviaSyntax)node.CreateRed(parent, position);
        red._parent = trivia;

        return red;
    }
}
