
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// A struct type declaration.
/// NOTE: This will be removed from the front end once classes are added.
/// (It will remain in the backend for code rewriting)
/// </summary>
internal sealed partial class StructDeclaration : TypeDeclaration {
    internal StructDeclaration(
        SyntaxTree syntaxTree, Token keyword, Token identifier, Token openBrace,
        SyntaxList<Member> members, Token closeBrace)
        : base(syntaxTree, keyword, identifier, openBrace, members, closeBrace) { }

    internal override SyntaxType type => SyntaxType.StructDeclaration;
}
