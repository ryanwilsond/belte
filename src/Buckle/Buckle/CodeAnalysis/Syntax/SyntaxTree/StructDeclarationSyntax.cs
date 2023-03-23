
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// A struct type declaration.
/// NOTE: This will be removed from the front end once classes are added.
/// (It will remain in the backend for code rewriting.)</br>
/// E.g.
/// <code>
/// struct StructName {
///     int a;
/// }
/// </code>
/// </summary>
internal sealed partial class StructDeclarationSyntax : TypeDeclarationSyntax {
    internal StructDeclarationSyntax(
        SyntaxTree syntaxTree, SyntaxToken keyword, SyntaxToken identifier, SyntaxToken openBrace,
        SyntaxList<MemberSyntax> members, SyntaxToken closeBrace)
        : base(syntaxTree, keyword, identifier, openBrace, members, closeBrace) { }

    internal new SyntaxToken keyword => base.keyword;

    internal new SyntaxToken identifier => base.identifier;

    internal new SyntaxToken openBrace => base.openBrace;

    internal new SyntaxList<MemberSyntax> members => base.members;

    internal new SyntaxToken closeBrace => base.closeBrace;

    internal override SyntaxKind kind => SyntaxKind.StructDeclaration;
}
