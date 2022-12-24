
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// A parameter <see cref="SyntaxNode" />.
/// </summary>
internal sealed partial class ParameterSyntax : SyntaxNode {
    /// <param name="typeClause"><see cref="TypeClauseSyntax" /> of the parameter.</param>
    /// <param name="identifier">Name of the parameter.</param>
    /// <returns>.</returns>
    internal ParameterSyntax(SyntaxTree syntaxTree, TypeClauseSyntax typeClause, SyntaxToken identifier)
        : base(syntaxTree) {
        this.typeClause = typeClause;
        this.identifier = identifier;
    }

    /// <summary>
    /// <see cref="TypeClauseSyntax" /> of the parameter.
    /// </summary>
    internal TypeClauseSyntax typeClause { get; }

    /// <summary>
    /// Name of the parameter.
    /// </summary>
    internal SyntaxToken identifier { get; }

    internal override SyntaxKind kind => SyntaxKind.Parameter;
}
