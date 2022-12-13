
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Function declaration (including body).
/// </summary>
internal sealed partial class FunctionDeclaration : Member {
    /// <param name="returnType"><see cref="TypeClause" /> of return type.</param>
    /// <param name="identifier">Name of the function.</param>
    internal FunctionDeclaration(
        SyntaxTree syntaxTree, TypeClause returnType, Token identifier, Token openParenthesis,
        SeparatedSyntaxList<Parameter> parameters, Token closeParenthesis, BlockStatement body)
        : base(syntaxTree) {
        this.returnType = returnType;
        this.identifier = identifier;
        this.openParenthesis = openParenthesis;
        this.parameters = parameters;
        this.closeParenthesis = closeParenthesis;
        this.body = body;
    }

    /// <summary>
    /// <see cref="TypeClause" /> of return type.
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