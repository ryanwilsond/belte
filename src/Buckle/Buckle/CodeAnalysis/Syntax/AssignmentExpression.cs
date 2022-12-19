
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Assignment expression, similar to an operator but assigns to an existing variable.
/// Thus cannot be used on literals.<br/>
/// E.g.
/// <code>
/// x = 4
/// </code>
/// </summary>
internal sealed partial class AssignmentExpression : Expression {
    /// <param name="left">What is being assigned.</param>
    /// <param name="right">Value to assign.</param>
    internal AssignmentExpression(SyntaxTree syntaxTree, Expression left, Token assignmentToken, Expression right)
        : base(syntaxTree) {
        this.left = left;
        this.assignmentToken = assignmentToken;
        this.right = right;
    }

    /// <summary>
    /// What is being assigned,
    /// </summary>
    internal Expression left { get; }

    internal Token assignmentToken { get; }

    /// <summary>
    /// Value to assign..
    /// </summary>
    internal Expression right { get; }

    internal override SyntaxType type => SyntaxType.AssignExpression;
}
