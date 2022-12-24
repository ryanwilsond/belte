
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// TypeOf expression (C#-Style).<br/>
/// E.g.
/// <code>
/// typeof(int)
/// </code>
/// </summary>
internal sealed partial class TypeOfExpressionSyntax : ExpressionSyntax {
    /// <param name="keyword">TypeOf keyword.</param>
    /// <param name="typeClause">The type to get the type type from.</param>
    internal TypeOfExpressionSyntax(
        SyntaxTree syntaxTree, SyntaxToken keyword, SyntaxToken openParenthesis,
        TypeClauseSyntax typeClause, SyntaxToken closeParenthesis)
        : base(syntaxTree) {
        this.keyword = keyword;
        this.openParenthesis = openParenthesis;
        this.typeClause = typeClause;
        this.closeParenthesis = closeParenthesis;
    }

    /// <summary>
    /// TypeOf keyword.
    /// </summary>
    internal SyntaxToken keyword { get;  }

    internal SyntaxToken openParenthesis { get;  }

    internal TypeClauseSyntax typeClause { get; }

    internal SyntaxToken closeParenthesis { get; }

    internal override SyntaxKind kind => SyntaxKind.TypeOfExpression;
}
