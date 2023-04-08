
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// A class type declaration.</br>
/// E.g.
/// <code>
/// class ClassName {
///     int a;
/// }
/// </code>
/// </summary>
internal sealed partial class ClassDeclarationSyntax : TypeDeclarationSyntax {
    internal ClassDeclarationSyntax(
        SyntaxTree syntaxTree, SyntaxToken keyword, SyntaxToken identifier, SyntaxToken openBrace,
        SyntaxList<MemberSyntax> members, SyntaxToken closeBrace)
        : base(syntaxTree, keyword, identifier, openBrace, members, closeBrace) { }

    internal new SyntaxToken keyword => base.keyword;

    internal new SyntaxToken identifier => base.identifier;

    internal new SyntaxToken openBrace => base.openBrace;

    internal new SyntaxList<MemberSyntax> members => base.members;

    internal new SyntaxToken closeBrace => base.closeBrace;

    internal override SyntaxKind kind => SyntaxKind.ClassDeclaration;
}

internal sealed partial class SyntaxFactory {
    internal ClassDeclarationSyntax ClassDeclaration(
        SyntaxToken keyword, SyntaxToken identifier, SyntaxToken openBrace,
        SyntaxList<MemberSyntax> members, SyntaxToken closeBrace)
        => Create(new ClassDeclarationSyntax(_syntaxTree, keyword, identifier, openBrace, members, closeBrace));
}
