
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Call expression, invokes a callable <see cref="Symbols.Symbol" /> (methods and functions).<br/>
/// E.g.
/// <code>
/// myFunc(...)
/// </code>
/// </summary>
internal sealed partial class CallExpressionSyntax : ExpressionSyntax {
    /// <param name="identifier">Name of the called method or function.</param>
    /// <param name="arguments">Parameter list.</param>
    internal CallExpressionSyntax(
        SyntaxTree syntaxTree, NameExpressionSyntax identifier, SyntaxToken openParenthesis,
        SeparatedSyntaxList<ArgumentSyntax> arguments, SyntaxToken closeParenthesis)
        : base(syntaxTree) {
        this.identifier = identifier;
        this.openParenthesis = openParenthesis;
        this.arguments = arguments;
        this.closeParenthesis = closeParenthesis;
    }

    /// <summary>
    /// Name of the called method or function.
    /// </summary>
    internal NameExpressionSyntax identifier { get; }

    internal SyntaxToken? openParenthesis { get; }

    /// <summary>
    /// Parameter list.
    /// </summary>
    internal SeparatedSyntaxList<ArgumentSyntax> arguments { get; }

    internal SyntaxToken? closeParenthesis { get; }

    internal override SyntaxKind kind => SyntaxKind.CallExpression;
}
