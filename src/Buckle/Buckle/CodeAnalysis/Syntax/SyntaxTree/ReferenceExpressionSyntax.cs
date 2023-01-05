
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Reference expression, returns the reference to a <see cref="Symbol" />.<br/>
/// E.g.
/// <code>
/// ref myVar
/// </code>
/// </summary>
internal sealed partial class ReferenceExpressionSyntax : ExpressionSyntax {
    /// <param name="keyword">Ref keyword.</param>
    /// <param name="identifier">Name of the referenced symbol.</param>
    internal ReferenceExpressionSyntax(SyntaxTree syntaxTree, SyntaxToken keyword, SyntaxToken identifier)
        : base(syntaxTree) {
        this.keyword = keyword;
        this.identifier = identifier;
    }

    /// <summary>
    /// Ref keyword.
    /// </summary>
    internal SyntaxToken keyword { get; }

    /// <summary>
    /// Name of the referenced <see cref="Symbol" />.
    /// </summary>
    internal SyntaxToken identifier { get; }

    internal override SyntaxKind kind => SyntaxKind.RefExpression;
}
