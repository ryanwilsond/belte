
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Expression statement, a statement that contains a single <see cref="ExpressionSyntax" /> and a semicolon.<br/>
/// E.g.
/// <code>
/// 4 + 3;
/// </code>
/// The allowed Expressions are:</br>
/// - <see cref="CallExpressionSyntax" />
/// - <see cref="AssignmentExpressionSyntax" />
/// - <see cref="EmptyExpressionSyntax" />
/// </summary>
internal sealed partial class ExpressionStatementSyntax : StatementSyntax {
    internal ExpressionStatementSyntax(SyntaxTree syntaxTree, ExpressionSyntax expression, SyntaxToken semicolon)
        : base(syntaxTree) {
        this.expression = expression;
        this.semicolon = semicolon;
    }

    internal ExpressionSyntax? expression { get; }

    internal SyntaxToken semicolon { get; }

    internal override SyntaxKind kind => SyntaxKind.ExpressionStatement;
}
