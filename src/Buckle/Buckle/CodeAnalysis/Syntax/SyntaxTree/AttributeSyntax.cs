
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// An attribute for a type.
/// </summary>
internal sealed partial class AttributeSyntax : SyntaxNode {
    /// <param name="identifier">Name of the attribute</param>
    internal AttributeSyntax(
        SyntaxTree syntaxTree, SyntaxToken openBracket, SyntaxToken identifier, SyntaxToken closeBracket)
        : base(syntaxTree) {
        this.openBracket = openBracket;
        this.identifier = identifier;
        this.closeBracket = closeBracket;
    }

    internal SyntaxToken openBracket { get; }

    /// <summary>
    /// Name of the attribute.
    /// </summary>
    internal SyntaxToken identifier { get; }

    internal SyntaxToken closeBracket { get; }

    internal override SyntaxKind kind => SyntaxKind.Attribute;
}
