
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// A parameter <see cref="SyntaxNode" />.
/// </summary>
internal sealed partial class ParameterSyntax : SyntaxNode {
    /// <param name="type"><see cref="TypeSyntax" /> of the parameter.</param>
    /// <param name="identifier">Name of the parameter.</param>
    /// <returns>.</returns>
    internal ParameterSyntax(SyntaxTree syntaxTree, TypeSyntax type, SyntaxToken identifier)
        : base(syntaxTree) {
        this.type = type;
        this.identifier = identifier;
    }

    /// <summary>
    /// <see cref="TypeSyntax" /> of the parameter.
    /// </summary>
    internal TypeSyntax type { get; }

    /// <summary>
    /// Name of the parameter.
    /// </summary>
    internal SyntaxToken identifier { get; }

    internal override SyntaxKind kind => SyntaxKind.Parameter;
}
