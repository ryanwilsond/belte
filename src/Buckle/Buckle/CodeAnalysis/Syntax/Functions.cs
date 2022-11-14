
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// A parameter node.
/// </summary>
internal sealed partial class Parameter : Node {
    /// <summary>
    /// Creates a parameter.
    /// </summary>
    /// <param name="syntaxTree">Syntax tree this node resides in</param>
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

    /// <summary>
    /// Type of node (see SyntaxType).
    /// </summary>
    internal override SyntaxType type => SyntaxType.PARAMETER;
}

/// <summary>
/// Function declaration (including body).
/// </summary>
internal sealed partial class FunctionDeclaration : Member {
    /// <summary>
    /// Creates a function declaration.
    /// </summary>
    /// <param name="syntaxTree">Syntax tree this node resides in</param>
    /// <param name="returnType">Type clause of return type</param>
    /// <param name="identifier">Name of the function</param>
    /// <param name="openParenthesis">Open parenthesis token</param>
    /// <param name="parameters">Parameter list</param>
    /// <param name="closeParenthesis">Close parenthesis token</param>
    /// <param name="body">Body of the function</param>
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

    /// <summary>
    /// Open parenthesis token.
    /// </summary>
    internal Token openParenthesis { get; }

    /// <summary>
    /// Parameter list.
    /// </summary>
    internal SeparatedSyntaxList<Parameter> parameters { get; }

    /// <summary>
    /// Close parenthesis token.
    /// </summary>
    internal Token closeParenthesis { get; }

    /// <summary>
    /// Body of the function.
    /// </summary>
    internal BlockStatement body { get; }

    /// <summary>
    /// Type of node (see SyntaxType).
    /// </summary>
    internal override SyntaxType type => SyntaxType.FUNCTION_DECLARATION;
}
