
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Expression, not a full line of code and most Expressions can be interchanged with most other Expressions.
/// </summary>
internal abstract class ExpressionSyntax : SyntaxNode {
    protected ExpressionSyntax(SyntaxTree syntaxTree) : base(syntaxTree) { }
}
