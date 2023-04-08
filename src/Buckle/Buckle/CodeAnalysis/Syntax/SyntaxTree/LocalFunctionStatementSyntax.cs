
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Local function statement, aka nested function declaration.
/// Syntactically identical to function declarations, but inside the scope of another function.
/// </summary>
internal sealed partial class LocalFunctionStatementSyntax : StatementSyntax {
    /// <param name="identifier">Name of the function.</param>
    internal LocalFunctionStatementSyntax(
        SyntaxTree syntaxTree, TypeSyntax returnType, SyntaxToken identifier, SyntaxToken openParenthesis,
        SeparatedSyntaxList<ParameterSyntax> parameters, SyntaxToken closeParenthesis, BlockStatementSyntax body)
        : base(syntaxTree) {
        this.returnType = returnType;
        this.identifier = identifier;
        this.openParenthesis = openParenthesis;
        this.parameters = parameters;
        this.closeParenthesis = closeParenthesis;
        this.body = body;
    }

    internal TypeSyntax returnType { get; }

    /// <summary>
    /// Name of the function.
    /// </summary>
    internal SyntaxToken identifier { get; }

    internal SyntaxToken openParenthesis { get; }

    internal SeparatedSyntaxList<ParameterSyntax> parameters { get; }

    internal SyntaxToken closeParenthesis { get; }

    internal BlockStatementSyntax body { get; }

    internal override SyntaxKind kind => SyntaxKind.LocalFunctionStatement;
}

internal sealed partial class SyntaxFactory {
    internal LocalFunctionStatementSyntax LocalFunctionStatement(
        TypeSyntax returnType, SyntaxToken identifier, SyntaxToken openParenthesis,
        SeparatedSyntaxList<ParameterSyntax> parameters, SyntaxToken closeParenthesis, BlockStatementSyntax body)
        => Create(new LocalFunctionStatementSyntax(
            _syntaxTree, returnType, identifier, openParenthesis, parameters, closeParenthesis, body
        ));
}
