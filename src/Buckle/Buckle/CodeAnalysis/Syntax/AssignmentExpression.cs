
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Assignment expression, similar to an operator but assigns to an existing variable.
/// Thus cannot be used on literals.
/// E.g. x = 4
/// </summary>
internal sealed partial class AssignmentExpression : Expression {
    /// <param name="identifier">Name of a variable.</param>
    /// <param name="expression">Value to set variable to.</param>
    internal AssignmentExpression(SyntaxTree syntaxTree, Token identifier, Token assignmentToken, Expression expression)
        : base(syntaxTree) {
        this.identifier = identifier;
        this.assignmentToken = assignmentToken;
        this.expression = expression;
    }

    /// <summary>
    /// Name of a variable.
    /// </summary>
    internal Token identifier { get; }

    internal Token assignmentToken { get; }

    /// <summary>
    /// Value to set variable to.
    /// </summary>
    internal Expression expression { get; }

    internal override SyntaxType type => SyntaxType.AssignExpression;
}
