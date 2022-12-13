
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Expression statement, a statement that contains a single <see cref="Expression" /> and a semicolon.
/// E.g. 4 + 3;
/// </summary>
internal sealed partial class ExpressionStatement : Statement {
    internal ExpressionStatement(SyntaxTree syntaxTree, Expression expression, Token semicolon) : base(syntaxTree) {
        this.expression = expression;
        this.semicolon = semicolon;
    }

    internal Expression? expression { get; }

    internal Token semicolon { get; }

    internal override SyntaxType type => SyntaxType.ExpressionStatement;
}
