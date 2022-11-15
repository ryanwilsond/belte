
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// A parameter node.
/// </summary>
internal sealed partial class Parameter : Node {
    /// <param name="typeClause">Type clause of the parameter</param>
    /// <param name="identifier">Name of the parameter</param>
    /// <returns></returns>
    internal Parameter(SyntaxTree syntaxTree, TypeClause typeClause, Token identifier) : base(syntaxTree) {
        this.typeClause = typeClause;
        this.identifier = identifier;
    }

    /// <summary>
    /// Type clause of the parameter.
    /// </summary>
    internal TypeClause typeClause { get; }

    /// <summary>
    /// Name of the parameter.
    /// </summary>
    internal Token identifier { get; }

    internal override SyntaxType type => SyntaxType.PARAMETER;
}

/// <summary>
/// Function declaration (including body).
/// </summary>
internal sealed partial class FunctionDeclaration : Member {
    /// <param name="returnType">Type clause of return type</param>
    /// <param name="identifier">Name of the function</param>
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

    internal override SyntaxType type => SyntaxType.FUNCTION_DECLARATION;
}
