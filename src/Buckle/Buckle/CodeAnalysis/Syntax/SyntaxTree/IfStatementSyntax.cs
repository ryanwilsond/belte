
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// If statement. Includes an optional <see cref="ElseClauseSyntax" />.<br/>
/// E.g.
/// <code>
/// if (condition) {
///     ... statements ...
/// } else {
///     ... statement ...
/// }
/// </code>
/// </summary>
internal sealed partial class IfStatementSyntax : StatementSyntax {
    /// <param name="keyword">If keyword.</param>
    /// <param name="condition">Condition <see cref="ExpressionSyntax" />, must be of type bool.</param>
    /// <param name="elseClause"><see cref="ElseClauseSyntax" /> (optional).</param>
    internal IfStatementSyntax(
        SyntaxTree syntaxTree, SyntaxToken keyword, SyntaxToken openParenthesis, ExpressionSyntax condition,
        SyntaxToken closeParenthesis, StatementSyntax then, ElseClauseSyntax elseClause)
        : base(syntaxTree) {
        this.keyword = keyword;
        this.openParenthesis = openParenthesis;
        this.condition = condition;
        this.closeParenthesis = closeParenthesis;
        this.then = then;
        this.elseClause = elseClause;
    }

    /// <summary>
    /// If keyword.
    /// </summary>
    internal SyntaxToken keyword { get; }

    internal SyntaxToken openParenthesis { get; }

    /// <summary>
    /// Condition <see cref="ExpressionSyntax" />, of type bool.
    /// </summary>
    internal ExpressionSyntax condition { get; }

    internal SyntaxToken closeParenthesis { get; }

    internal StatementSyntax then { get; }

    /// <summary>
    /// <see cref="ElseClauseSyntax" /> (includes keyword and body).
    /// </summary>
    internal ElseClauseSyntax? elseClause { get; }

    internal override SyntaxKind kind => SyntaxKind.IfStatement;
}

internal sealed partial class SyntaxFactory {
    internal IfStatementSyntax IfStatement(
        SyntaxToken keyword, SyntaxToken openParenthesis, ExpressionSyntax condition,
        SyntaxToken closeParenthesis, StatementSyntax then, ElseClauseSyntax elseClause)
        => Create(new IfStatementSyntax(
            _syntaxTree, keyword, openParenthesis, condition, closeParenthesis, then, elseClause
        ));
}
