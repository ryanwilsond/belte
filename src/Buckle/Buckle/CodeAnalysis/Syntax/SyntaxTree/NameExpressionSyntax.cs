
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Name expression, references a <see cref="Symbols.Symbol" />.<br/>
/// E.g.
/// <code>
/// myVar
/// </code>
/// </summary>
internal sealed partial class NameExpressionSyntax : ExpressionSyntax {
    /// <param name="identifier">Name of the symbol.</param>
    internal NameExpressionSyntax(SyntaxTree syntaxTree, SyntaxToken identifier) : base(syntaxTree) {
        this.identifier = identifier;
    }

    public override SyntaxKind kind => SyntaxKind.NameExpression;

    /// <summary>
    /// Name of the <see cref="Symbols.Symbol" />.
    /// </summary>
    internal SyntaxToken identifier { get; }
}

internal sealed partial class SyntaxFactory {
    internal NameExpressionSyntax NameExpression(SyntaxToken identifier)
        => Create(new NameExpressionSyntax(_syntaxTree, identifier));
}
