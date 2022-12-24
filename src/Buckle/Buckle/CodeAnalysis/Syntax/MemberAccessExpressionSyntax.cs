
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
        SyntaxTree syntaxTree, ExpressionSyntax operand, SyntaxToken period, SyntaxToken identifer)
        : base(syntaxTree) {
        this.operand = operand;
        this.period = period;
        this.identifier = identifer;
    }

    internal ExpressionSyntax operand { get; }

    internal SyntaxToken period { get; }

    /// <summary>
    /// Name of the member to access.
    /// </summary>
    internal SyntaxToken? identifier { get; }

    internal override SyntaxKind kind => SyntaxKind.MemberAccessExpression;
}
