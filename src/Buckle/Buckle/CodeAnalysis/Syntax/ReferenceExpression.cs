
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Reference expression, returns the reference to a <see cref="Symbol" />.
/// E.g. ref myVar
/// </summary>
internal sealed partial class ReferenceExpression : Expression {
    /// <param name="identifier">Name of the referenced symbol.</param>
    internal ReferenceExpression(SyntaxTree syntaxTree, Token refKeyword, Token identifier) : base(syntaxTree) {
        this.refKeyword = refKeyword;
        this.identifier = identifier;
    }

    internal Token refKeyword { get; }

    /// <summary>
    /// Name of the referenced <see cref="Symbol" />.
    /// </summary>
    internal Token identifier { get; }

    internal override SyntaxType type => SyntaxType.RefExpression;
}
