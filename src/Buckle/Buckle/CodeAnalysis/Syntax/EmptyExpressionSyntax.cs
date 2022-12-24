
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Empty expression, used as debugging expressions and placeholders.
/// Can only be created in a source file by creating an <see cref="ExpressionStatementSyntax" /> with an
/// <see cref="EmptyExpressionSyntax" />:<br/>
/// <code>
///     ;
/// </code>
/// </summary>
internal sealed partial class EmptyExpressionSyntax : ExpressionSyntax {
    internal EmptyExpressionSyntax(SyntaxTree syntaxTree) : base(syntaxTree) { }

    internal override SyntaxKind kind => SyntaxKind.EmptyExpression;
}
