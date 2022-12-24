
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Assignment expression, similar to an operator but assigns to an existing variable.
/// Thus cannot be used on literals.<br/>
/// E.g.
/// <code>
/// x = 4
/// </code>
/// </summary>
internal sealed partial class AssignmentExpressionSyntax : ExpressionSyntax {
    /// <param name="left">What is being assigned.</param>
    /// <param name="right">Value to assign.</param>
    internal AssignmentExpressionSyntax(
        SyntaxTree syntaxTree, ExpressionSyntax left, SyntaxToken assignmentToken, ExpressionSyntax right)
        : base(syntaxTree) {
        this.left = left;
        this.assignmentToken = assignmentToken;
        this.right = right;
    }

    /// <summary>
    /// What is being assigned,
    /// </summary>
    internal ExpressionSyntax left { get; }

    internal SyntaxToken assignmentToken { get; }

    /// <summary>
    /// Value to assign..
    /// </summary>
    internal ExpressionSyntax right { get; }

    internal override SyntaxKind kind => SyntaxKind.AssignExpression;
}
