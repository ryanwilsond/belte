
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// An argument used in a <see cref="CallExpressionSyntax" />.
/// </summary>
internal sealed partial class ArgumentSyntax : SyntaxNode {
    /// <param name="name">
    /// Optional; name if the argument is referencing a parameter by name instead of by ordinal.
    /// </param>
    /// <param name="colon">
    /// Optional; used to separate the name from the expression, always and only used if <param name="name" /> is
    /// specified.
    /// </param>
    /// <param name="expression">Value of the argument.</param>
    internal ArgumentSyntax(
        SyntaxTree syntaxTree, SyntaxToken name, SyntaxToken colon, ExpressionSyntax expression)
        : base(syntaxTree) {
        this.name = name;
        this.colon = colon;
        this.expression = expression;
    }

    /// <summary>
    /// Optional; name if the argument is referencing a parameter by name instead of by ordinal.
    /// </summary>
    internal SyntaxToken? name { get; }

    /// <summary>
    /// Optional; used to separate the name from the expression, always and only used if <param name="name" /> is
    /// specified.
    /// </summary>
    internal SyntaxToken? colon { get; }

    /// <summary>
    /// Value of the argument.
    /// </summary>
    internal ExpressionSyntax expression { get; }

    internal override SyntaxKind kind => SyntaxKind.Argument;
}

internal sealed partial class SyntaxFactory {
    internal ArgumentSyntax Argument(SyntaxToken name, SyntaxToken colon, ExpressionSyntax expression)
        => Create(new ArgumentSyntax(_syntaxTree, name, colon, expression));
}
