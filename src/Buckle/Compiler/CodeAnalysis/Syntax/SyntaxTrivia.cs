using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// All trivia: comments and whitespace. Text that does not affect compilation.
/// </summary>
public class SyntaxTrivia {
    /// <summary>
    /// Creates a new <see cref="SyntaxTrivia" /> with an existing token, underlying trivia, and position.
    /// Also contains the index of this trivia in relation to other trivias in the trivia list that contains
    /// this trivia.
    /// </summary>
    internal SyntaxTrivia(SyntaxToken token, GreenNode trivia, int position, int index) {
        this.token = token;
        green = trivia;
        this.position = position;
        this.index = index;
    }

    /// <summary>
    /// The token that this trivia is wrapping.
    /// </summary>
    internal SyntaxToken token { get; }

    /// <summary>
    /// The underlying trivia.
    /// </summary>
    internal GreenNode green { get; }

    /// <summary>
    /// The index that this trivia is in relation to an owning trivia list.
    /// </summary>
    internal int index { get; }

    /// <summary>
    /// The start position of this trivia.
    /// </summary>
    internal int position { get; }

    /// <summary>
    /// The width of the trivia. Is the same as <see cref="fullWidth" />.
    /// </summary>
    internal int width => green?.width ?? 0;

    /// <summary>
    /// The full width of the trivia. Is the same as <see cref="width" />.
    /// </summary>
    internal int fullWidth => green?.width ?? 0;

    /// <summary>
    /// The kind of trivia.
    /// </summary>
    internal SyntaxKind kind => green?.kind ?? SyntaxKind.None;

    /// <summary>
    /// The span of the trivia. Should be the same as <see cref="fullSpan" />.
    /// </summary>
    /// <returns></returns>
    internal TextSpan span => green != null
        ? new TextSpan(position + green.GetLeadingTriviaWidth(), green.width)
        : null;

    /// <summary>
    /// The full span of the trivia. Should be the same as <see cref="span" />.
    /// </summary>
    /// <returns></returns>
    internal TextSpan fullSpan => green != null ? new TextSpan(position, green.fullWidth) : null;

    /// <summary>
    /// If the trivia contains any diagnostics.
    /// </summary>
    internal bool containsDiagnostics => green?.containsDiagnostics ?? false;

    /// <summary>
    /// The <see cref="SyntaxTree" /> that contains this trivia.
    /// </summary>
    internal SyntaxTree syntaxTree => token.syntaxTree;

    /// <summary>
    /// The location of this trivia in the <see cref="SourceText" /> that contains it.
    /// </summary>
    /// <returns></returns>
    internal TextLocation location => new TextLocation(syntaxTree.text, span);
}
