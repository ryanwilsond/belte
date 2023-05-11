using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// All trivia: comments and whitespace. Text that does not affect compilation.
/// </summary>
internal sealed class SyntaxTrivia {
    internal SyntaxTrivia(SyntaxToken token, GreenNode trivia, int position, int index) {
        this.token = token;
        green = trivia;
        this.position = position;
        this.index = index;
    }

    internal SyntaxToken token { get; }

    internal GreenNode green { get; }

    internal int position { get; }

    internal int index { get; }

    internal int width => green?.width ?? 0;

    internal int fullWidth => green?.width ?? 0;

    internal TextSpan span => green != null
        ? new TextSpan(position + green.GetLeadingTriviaWidth(), green.width)
        : null;

    internal TextSpan fullSpan => green != null ? new TextSpan(position, green.fullWidth) : null;

    internal bool containsDiagnostics => green?.containsDiagnostics ?? false;

    internal SyntaxTree syntaxTree => token.syntaxTree;

    internal TextLocation location => new TextLocation(syntaxTree.text, span);
}
