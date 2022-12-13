using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// TypeOf expression (C#-Style).<br/>
/// E.g.
/// <code>
/// typeof(int)
/// </code>
/// </summary>
internal sealed partial class TypeOfExpression : Expression {
    /// <param name="keyword">TypeOf keyword.</param>
    /// <param name="typeCLause">The type to get the type type from.</param>
    internal TypeOfExpression(
        SyntaxTree syntaxTree, Token keyword, Token openParenthesis,
        TypeClause typeClause, Token closeParenthesis)
        : base(syntaxTree) {
        this.keyword = keyword;
        this.openParenthesis = openParenthesis;
        this.typeClause = typeClause;
        this.closeParenthesis = closeParenthesis;
    }

    /// <summary>
    /// TypeOf keyword.
    /// </summary>
    internal Token keyword { get;  }

    internal Token openParenthesis { get;  }

    internal TypeClause typeClause { get; }

    internal Token closeParenthesis { get; }

    internal override SyntaxType type => SyntaxType.TypeOfExpression;
}
