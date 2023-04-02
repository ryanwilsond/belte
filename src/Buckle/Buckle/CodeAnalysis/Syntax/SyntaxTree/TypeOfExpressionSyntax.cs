
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
    /// <param name="type">The type to get the type type from.</param>
    internal TypeOfExpressionSyntax(
        SyntaxTree syntaxTree, SyntaxToken keyword, SyntaxToken openParenthesis,
        TypeSyntax type, SyntaxToken closeParenthesis)
        : base(syntaxTree) {
        this.keyword = keyword;
        this.openParenthesis = openParenthesis;
        this.type = type;
        this.closeParenthesis = closeParenthesis;
    }

    /// <summary>
    /// TypeOf keyword.
    /// </summary>
    internal SyntaxToken keyword { get;  }

    internal SyntaxToken openParenthesis { get;  }

    internal TypeSyntax type { get; }

    internal SyntaxToken closeParenthesis { get; }

    internal override SyntaxKind kind => SyntaxKind.TypeOfExpression;
}

internal sealed partial class SyntaxFactory {
    internal TypeOfExpressionSyntax TypeOfExpression(
        SyntaxToken keyword, SyntaxToken openParenthesis, TypeSyntax type, SyntaxToken closeParenthesis) =>
        Create(new TypeOfExpressionSyntax(_syntaxTree, keyword, openParenthesis, type, closeParenthesis));
}
