
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Call expression, invokes a callable <see cref="Symbol" /> (function).<br/>
/// E.g.
/// <code>
/// myFunc(...)
/// </code>
/// </summary>
internal sealed partial class CallExpression : Expression {
    /// <param name="identifier">Name of the called function.</param>
    /// <param name="arguments">Parameter list.</param>
    internal CallExpression(
        SyntaxTree syntaxTree, NameExpression identifier, Token openParenthesis,
        SeparatedSyntaxList<Expression> arguments, Token closeParenthesis)
        : base(syntaxTree) {
        this.identifier = identifier;
        this.openParenthesis = openParenthesis;
        this.arguments = arguments;
        this.closeParenthesis = closeParenthesis;
    }

    /// <summary>
    /// Name of the called function.
    /// </summary>
    internal NameExpression identifier { get; }

    internal Token? openParenthesis { get; }

    /// <summary>
    /// Parameter list.
    /// </summary>
    internal SeparatedSyntaxList<Expression> arguments { get; }

    internal Token? closeParenthesis { get; }

    internal override SyntaxType type => SyntaxType.CallExpression;
}
