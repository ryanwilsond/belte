
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// A struct declaration.
/// NOTE: This will be removed from the front end once classes are added.
/// (It will remain in the backend for code rewriting)
/// </summary>
internal sealed partial class StructDeclaration : Member {
    internal StructDeclaration(
        SyntaxTree syntaxTree, Token structKeyword, Token identifier, Token openBrace,
        SyntaxList<Member> parameters, Token closeBrace)
        : base(syntaxTree) {
        this.returnType = returnType;
        this.identifier = identifier;
        this.openParenthesis = openParenthesis;
        this.parameters = parameters;
        this.closeParenthesis = closeParenthesis;
        this.body = body;
    }

    /// <summary>
    /// Type clause of return type.
    /// </summary>
    internal TypeClause returnType { get; }

    /// <summary>
    /// Name of the function.
    /// </summary>
    internal Token identifier { get; }

    internal Token openParenthesis { get; }

    internal SeparatedSyntaxList<Parameter> parameters { get; }

    internal Token closeParenthesis { get; }

    internal BlockStatement body { get; }

    internal override SyntaxType type => SyntaxType.MethodDeclaration;
}
