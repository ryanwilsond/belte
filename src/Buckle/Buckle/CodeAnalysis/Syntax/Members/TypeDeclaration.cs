
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// A type declaration (currently just <see cref="StructDeclaration" />).
/// </summary>
internal abstract partial class TypeDeclaration : Member {
    internal TypeDeclaration(
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
    /// Keyword ("struct", thats currently the only option).
    /// </summary>
    internal Token keyword { get; }

    /// <summary>
    /// The name of the type.
    /// </summary>
    internal Token identifier { get; }

    internal Token openBrace { get; }

    internal SyntaxList<Member> members { get; }

    internal Token closeBrace { get; }
}
