using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Typeof expression (C#-Style).
/// E.g. typeof(int)
/// </summary>
internal sealed partial class TypeOfExpression : Expression {
    /// <param name="typeCLause">The type to get the type type from.</param>
    internal TypeOfExpression(
        SyntaxTree syntaxTree, Token typeofKeyword, Token openParenthesis,
        TypeClause typeClause, Token closeParenthesis)
        : base(syntaxTree) {
        this.typeofKeyword = typeofKeyword;
        this.openParenthesis = openParenthesis;
        this.typeClause = typeClause;
        this.closeParenthesis = closeParenthesis;
    }

    internal Token typeofKeyword { get;  }

    internal Token openParenthesis { get;  }

    internal TypeClause typeClause { get; }

    internal Token closeParenthesis { get; }

    internal override SyntaxType type => SyntaxType.TypeOfExpression;
}
