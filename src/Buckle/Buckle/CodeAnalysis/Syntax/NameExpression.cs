
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Name expression, references a symbol (variable or function).
/// E.g. myVar
/// </summary>
internal sealed partial class NameExpression : Expression {
    /// <param name="identifier">Name of the symbol</param>
    internal NameExpression(SyntaxTree syntaxTree, Token identifier) : base(syntaxTree) {
        this.identifier = identifier;
    }

    /// <summary>
    /// Name of the symbol.
    /// </summary>
    internal Token identifier { get; }

    internal override SyntaxType type => SyntaxType.NameExpression;
}
