
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Literal expression, such as a number or a string.<br/>
/// E.g.
/// <code>
/// "Hello, world!"
/// 34.6
/// </code>
/// </summary>
internal sealed partial class LiteralExpression : Expression {
    internal LiteralExpression(SyntaxTree syntaxTree, Token token, object value) : base(syntaxTree) {
        this.token = token;
        this.value = value;
    }

    internal LiteralExpression(SyntaxTree syntaxTree, Token token) : this(syntaxTree, token, token.value) { }

    internal Token token { get; }

    internal object value { get; }

    internal override SyntaxType type => SyntaxType.LiteralExpression;
}
