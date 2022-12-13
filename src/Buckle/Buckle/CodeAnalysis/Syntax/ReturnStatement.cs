
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Return statement. Only used in function bodies, or scopes that are inside a function body.
/// Have an optional return value.
/// E.g. return 3;
/// </summary>
internal sealed partial class ReturnStatement : Statement {
    /// <param name="expression">Return value (optional).</param>
    internal ReturnStatement(SyntaxTree syntaxTree, Token keyword, Expression expression, Token semicolon)
        : base(syntaxTree) {
        this.keyword = keyword;
        this.expression = expression;
        this.semicolon = semicolon;
    }

    internal Token keyword { get; }

    /// <summary>
    /// Return value (optional).
    /// </summary>
    internal Expression? expression { get; }

    internal Token semicolon { get; }

    internal override SyntaxType type => SyntaxType.ReturnStatement;
}
