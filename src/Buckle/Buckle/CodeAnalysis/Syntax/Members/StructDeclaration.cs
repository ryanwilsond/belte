
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
internal sealed partial class StructDeclaration : TypeDeclaration {
    internal StructDeclaration(
        SyntaxTree syntaxTree, Token keyword, Token identifier, Token openBrace,
        SyntaxList<Member> members, Token closeBrace)
        : base(syntaxTree, keyword, identifier, openBrace, members, closeBrace) { }

    internal new Token keyword => base.keyword;

    internal new Token identifier => base.identifier;

    internal new Token openBrace => base.openBrace;

    internal new SyntaxList<Member> members => base.members;

    internal new Token closeBrace => base.closeBrace;

    internal override SyntaxType type => SyntaxType.StructDeclaration;
}
