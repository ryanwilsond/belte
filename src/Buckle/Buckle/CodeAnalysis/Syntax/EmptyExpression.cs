
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Empty expression, used as debugging expressions and placeholders.
/// Can only be created in a source file by creating an <see cref="ExpressionStatement" /> with an
/// <see cref="EmptyExpression" />:<br/>
/// <code>
///     ;
/// </code>
/// </summary>
internal sealed partial class EmptyExpression : Expression {
    internal EmptyExpression(SyntaxTree syntaxTree) : base(syntaxTree) { }

    internal override SyntaxType type => SyntaxType.EmptyExpression;
}
