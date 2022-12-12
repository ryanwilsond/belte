using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// All trivia: comments and whitespace. Text that does not affect compilation.
/// </summary>
internal sealed class SyntaxTrivia {
    /// <param name="position">Position of the trivia (indexed by nodes, not by character)</param>
    /// <param name="text">Text associated with the trivia</param>
    internal SyntaxTrivia(SyntaxTree syntaxTree, SyntaxType type, int position, string text) {
        this.syntaxTree = syntaxTree;
        this.position = position;
        this.type = type;
        this.text = text;
    }

    internal SyntaxTree syntaxTree { get; }

    internal SyntaxType type { get; }

    /// <summary>
    /// The position of the trivia.
    /// </summary>
    internal int position { get; }

    /// <summary>
    /// The span of where the trivia is in the source text.
    /// </summary>
    internal TextSpan span => new TextSpan(position, text?.Length ?? 0);

    /// <summary>
    /// Text associated with the trivia.
    /// </summary>
    internal string text { get; }
}
