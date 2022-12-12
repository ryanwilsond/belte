using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Local function statement, aka nested function declaration.
/// Syntactically identical to function declarations, but inside the scope of another function.
/// </summary>
internal sealed partial class LocalFunctionStatement : Statement {
    /// <param name="identifier">Name of the function</param>
    internal LocalFunctionStatement(
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

    internal TypeClause returnType { get; }

    /// <summary>
    /// Name of the function.
    /// </summary>
    internal Token identifier { get; }

    internal Token openParenthesis { get; }

    internal SeparatedSyntaxList<Parameter> parameters { get; }

    internal Token closeParenthesis { get; }

    internal BlockStatement body { get; }

    internal override SyntaxType type => SyntaxType.LocalFunctionStatement;
}
