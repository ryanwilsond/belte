
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// A type declaration (currently just <see cref="StructDeclarationSyntax" />).
/// </summary>
internal abstract partial class TypeDeclarationSyntax : MemberSyntax {
    internal TypeDeclarationSyntax(
        SyntaxTree syntaxTree, SyntaxToken keyword, SyntaxToken identifier, SyntaxToken openBrace,
        SyntaxList<MemberSyntax> members, SyntaxToken closeBrace)
        : base(syntaxTree) {
        this.keyword = keyword;
        this.identifier = identifier;
        this.openBrace = openBrace;
        this.members = members;
        this.closeBrace = closeBrace;
    }

    /// <summary>
    /// Keyword ("struct", thats currently the only option).
    /// </summary>
    internal SyntaxToken keyword { get; }

    /// <summary>
    /// The name of the type.
    /// </summary>
    internal SyntaxToken identifier { get; }

    internal SyntaxToken openBrace { get; }

    internal SyntaxList<MemberSyntax> members { get; }

    internal SyntaxToken closeBrace { get; }
}
