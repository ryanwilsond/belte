
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Expression statement, a statement that contains a single <see cref="ExpressionSyntax" /> and a semicolon.<br/>
/// E.g.
/// <code>
/// 4 + 3;
/// </code>
/// The allowed Expressions are:</br>
/// - <see cref="CallExpressionSyntax" /></br>
/// - <see cref="AssignmentExpression" /></br>
/// - <see cref="EmptyExpression" /></br>
/// - <see cref="ErrorExpression" /></br>
/// - <see cref="CompoundAssignmentExpression" />
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
