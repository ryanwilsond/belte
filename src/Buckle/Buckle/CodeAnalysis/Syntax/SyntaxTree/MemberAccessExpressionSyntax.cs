
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
        SyntaxTree syntaxTree, ExpressionSyntax operand, SyntaxToken op, SyntaxToken identifier)
        : base(syntaxTree) {
        this.operand = operand;
        this.op = op;
        this.identifier = identifier;
    }

    public override SyntaxKind kind => SyntaxKind.MemberAccessExpression;

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
}

internal sealed partial class SyntaxFactory {
    internal MemberAccessExpressionSyntax MemberAccessExpression(
        ExpressionSyntax operand, SyntaxToken op, SyntaxToken identifier)
        => Create(new MemberAccessExpressionSyntax(_syntaxTree, operand, op, identifier));
}
