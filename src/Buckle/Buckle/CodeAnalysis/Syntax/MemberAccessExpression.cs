
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Member access expression, by reference not by value.<br/>
/// E.g.
/// <code>
/// myType.myMember
/// </code>
/// </summary>
internal sealed partial class MemberAccessExpression : Expression {
    /// <param name="identifier">Name of the member to access.</param>
    internal MemberAccessExpression(SyntaxTree syntaxTree, Expression operand, Token period, Token identifer)
        : base(syntaxTree) {
        this.operand = operand;
        this.period = period;
        this.identifier = identifer;
    }

    internal Expression operand { get; }

    internal Token period { get; }

    /// <summary>
    /// Name of the member to access.
    /// </summary>
    internal Token? identifier { get; }

    internal override SyntaxType type => SyntaxType.MemberAccessExpression;
}
