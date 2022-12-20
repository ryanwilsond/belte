
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Name expression, references a <see cref="Symbol" /> (variable or function).<br/>
/// E.g.
/// <code>
/// myVar
/// </code>
/// </summary>
internal sealed partial class NameExpression : Expression {
    /// <param name="identifier">Name of the symbol.</param>
    internal NameExpression(SyntaxTree syntaxTree, Token identifier) : base(syntaxTree) {
        this.identifier = identifier;
    }

    /// <summary>
    /// Name of the <see cref="Symbol" />.
    /// </summary>
    internal Token identifier { get; }

    internal override SyntaxType type => SyntaxType.NameExpression;
}
