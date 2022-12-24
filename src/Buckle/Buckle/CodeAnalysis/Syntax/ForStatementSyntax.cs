
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// For statement, uses 3 part system, not for each.<br/>
/// E.g.
/// <code>
/// for (iterator declaration; condition; step) {
///     ... statements ...
/// }
/// </code>
/// </summary>
internal sealed partial class ForStatementSyntax : StatementSyntax {
    /// <param name="initializer">Declaration or name of variable used for stepping.</param>
    internal ForStatementSyntax(
        SyntaxTree syntaxTree, SyntaxToken keyword, SyntaxToken openParenthesis, StatementSyntax initializer,
        ExpressionSyntax condition, SyntaxToken semicolon, ExpressionSyntax step,
        SyntaxToken closeParenthesis, StatementSyntax body)
        : base(syntaxTree) {
        this.keyword = keyword;
        this.openParenthesis = openParenthesis;
        this.initializer = initializer;
        this.condition = condition;
        this.semicolon = semicolon;
        this.step = step;
        this.closeParenthesis = closeParenthesis;
        this.body = body;
    }

    internal SyntaxToken keyword { get; }

    internal SyntaxToken openParenthesis { get; }

    /// <summary>
    /// Declaration or name of variable used for stepping.
    /// </summary>
    internal StatementSyntax initializer { get; }

    internal ExpressionSyntax condition { get; }

    internal SyntaxToken semicolon { get; }

    internal ExpressionSyntax step { get; }

    internal SyntaxToken closeParenthesis { get; }

    internal StatementSyntax body { get; }

    internal override SyntaxKind kind => SyntaxKind.ForStatement;
}
