
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// A parameter <see cref="Node" />.
/// </summary>
internal sealed partial class Parameter : Node {
    /// <param name="typeClause"><see cref="TypeClause" /> of the parameter.</param>
    /// <param name="identifier">Name of the parameter.</param>
    /// <returns>.</returns>
    internal Parameter(SyntaxTree syntaxTree, TypeClause typeClause, Token identifier) : base(syntaxTree) {
        this.typeClause = typeClause;
        this.identifier = identifier;
    }

    /// <summary>
    /// <see cref="TypeClause" /> of the parameter.
    /// </summary>
    internal TypeClause typeClause { get; }

    /// <summary>
    /// Name of the parameter.
    /// </summary>
    internal Token identifier { get; }

    internal override SyntaxType type => SyntaxType.Parameter;
}
