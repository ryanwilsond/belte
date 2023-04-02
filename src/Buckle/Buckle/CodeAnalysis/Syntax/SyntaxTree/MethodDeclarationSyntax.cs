
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Method declaration (including body).
/// E.g.
/// <code>
/// void MethodName(int a) {
///     Print(a);
/// }
/// </code>
/// </summary>
internal sealed partial class MethodDeclarationSyntax : MemberSyntax {
    /// <param name="returnType"><see cref="TypeSyntax" /> of return type.</param>
    /// <param name="identifier">Name of the method.</param>
    internal MethodDeclarationSyntax(
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

    /// <summary>
    /// <see cref="TypeSyntax" /> of return type.
    /// </summary>
    internal TypeSyntax returnType { get; }

    /// <summary>
    /// Name of the method.
    /// </summary>
    internal SyntaxToken identifier { get; }

    internal SyntaxToken openParenthesis { get; }

    internal SeparatedSyntaxList<ParameterSyntax> parameters { get; }

    internal SyntaxToken closeParenthesis { get; }

    internal BlockStatementSyntax body { get; }

    internal override SyntaxKind kind => SyntaxKind.MethodDeclaration;
}

internal sealed partial class SyntaxFactory {
    internal MethodDeclarationSyntax MethodDeclaration(
        TypeSyntax returnType, SyntaxToken identifier, SyntaxToken openParenthesis,
        SeparatedSyntaxList<ParameterSyntax> parameters, SyntaxToken closeParenthesis, BlockStatementSyntax body) =>
        Create(new MethodDeclarationSyntax(
            _syntaxTree, returnType, identifier, openParenthesis, parameters, closeParenthesis, body
        ));
}
