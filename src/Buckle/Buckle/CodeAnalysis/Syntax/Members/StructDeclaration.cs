
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// A struct declaration.
/// NOTE: This will be removed from the front end once classes are added.
/// (It will remain in the backend for code rewriting)
/// </summary>
internal sealed partial class StructDeclaration : Member {
    internal StructDeclaration(
        SyntaxTree syntaxTree, Token keyword, Token identifier, Token openBrace,
        SyntaxList<Member> members, Token closeBrace)
        : base(syntaxTree) {
        this.keyword = keyword;
        this.identifier = identifier;
        this.openBrace = openBrace;
        this.members = members;
        this.closeBrace = closeBrace;
    }

    /// <summary>
    /// Struct keyword.
    /// </summary>
    internal Token keyword { get; }

    /// <summary>
    /// The name of the struct.
    /// </summary>
    internal Token identifier { get; }

    internal Token openBrace { get; }

    internal SyntaxList<Parameter> members { get; }

    internal Token closeBrace { get; }

    internal override SyntaxType type => SyntaxType.StructDeclaration;
}
