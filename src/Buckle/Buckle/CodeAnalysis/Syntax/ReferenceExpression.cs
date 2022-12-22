
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Reference expression, returns the reference to a <see cref="Symbol" />.<br/>
/// E.g.
/// <code>
/// ref myVar
/// </code>
/// </summary>
internal sealed partial class ReferenceExpression : Expression {
    /// <param name="keyword">Ref keyword.</param>
    /// <param name="identifier">Name of the referenced symbol.</param>
    internal ReferenceExpression(SyntaxTree syntaxTree, Token keyword, Token identifier) : base(syntaxTree) {
        this.keyword = keyword;
        this.identifier = identifier;
    }

    /// <summary>
    /// Ref keyword.
    /// </summary>
    internal Token keyword { get; }

    /// <summary>
    /// Name of the referenced <see cref="Symbol" />.
    /// </summary>
    internal Token identifier { get; }

    internal override SyntaxType type => SyntaxType.RefExpression;
}
