
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// A parameter node.
/// </summary>
internal sealed partial class Parameter : Node {
    /// <param name="typeClause">Type clause of the parameter</param>
    /// <param name="identifier">Name of the parameter</param>
    /// <returns></returns>
    internal Parameter(SyntaxTree syntaxTree, TypeClause typeClause, Token identifier) : base(syntaxTree) {
        this.typeClause = typeClause;
        this.identifier = identifier;
    }

    /// <summary>
    /// Type clause of the parameter.
    /// </summary>
    internal TypeClause typeClause { get; }

    /// <summary>
    /// Name of the parameter.
    /// </summary>
    internal Token identifier { get; }

    internal override SyntaxType type => SyntaxType.Parameter;
}
