
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Name expression, references a <see cref="Symbol" /> (variable or function).<br/>
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

    /// <summary>
    /// Name of the <see cref="Symbol" />.
    /// </summary>
    internal SyntaxToken identifier { get; }

    internal override SyntaxKind kind => SyntaxKind.NameExpression;
}
