
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Member access expression, by reference not by value.<br/>
/// E.g.
/// <code>
/// myType.myMember
/// </code>
/// </summary>
internal sealed partial class MemberAccessExpressionSyntax : ExpressionSyntax {
    /// <param name="identifier">Name of the member to access.</param>
    internal MemberAccessExpressionSyntax(
        SyntaxTree syntaxTree, ExpressionSyntax operand, SyntaxToken op, SyntaxToken identifer)
        : base(syntaxTree) {
        this.operand = operand;
        this.op = op;
        this.identifier = identifer;
    }

    internal ExpressionSyntax operand { get; }

    /// <summary>
    /// Member access operator, either <see cref="SyntaxKind.PeriodToken" /> or
    /// <see cref="SyntaxKind.QuestionPeriodToken" /> (null-conditional member access).
    /// </summary>
    internal SyntaxToken op { get; }

    /// <summary>
    /// Name of the member to access.
    /// </summary>
    internal SyntaxToken? identifier { get; }

    internal override SyntaxKind kind => SyntaxKind.MemberAccessExpression;
}
