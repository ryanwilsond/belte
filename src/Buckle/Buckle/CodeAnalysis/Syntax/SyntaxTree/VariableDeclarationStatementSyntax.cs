
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Variable declaration, definition is optional.<br/>
/// E.g.
/// <code>
/// int myVar = 3;
/// </code>
/// </summary>
internal sealed partial class VariableDeclarationStatementSyntax : StatementSyntax {
    /// <param name="type"><see cref="TypeSyntax" /> of the variable being declared.</param>
    /// <param name="identifier">Name of the variable.</param>
    /// <param name="equals">Equals <see cref="SyntaxToken" /> (optional).</param>
    /// <param name="initializer">Definition value (optional).</param>
    internal VariableDeclarationStatementSyntax(
        SyntaxTree syntaxTree, TypeSyntax type,
        SyntaxToken identifier, SyntaxToken equals, ExpressionSyntax initializer, SyntaxToken semicolon)
        : base(syntaxTree) {
        this.type = type;
        this.identifier = identifier;
        this.equals = equals;
        this.initializer = initializer;
        this.semicolon = semicolon;
    }

    public override SyntaxKind kind => SyntaxKind.VariableDeclarationStatement;

    /// <summary>
    /// <see cref="TypeSyntax" /> of the variable being declared.
    /// </summary>
    internal TypeSyntax type { get; }

    /// <summary>
    /// Name of the variable.
    /// </summary>
    internal SyntaxToken identifier { get; }

    /// <summary>
    /// Equals <see cref="SyntaxToken" /> (optional).
    /// </summary>
    internal SyntaxToken? equals { get; }

    /// <summary>
    /// Definition value (optional).
    /// </summary>
    internal ExpressionSyntax? initializer { get; }

    internal SyntaxToken semicolon { get; }
}

internal sealed partial class SyntaxFactory {
    internal VariableDeclarationStatementSyntax VariableDeclarationStatement(
        TypeSyntax type, SyntaxToken identifier, SyntaxToken equals,
        ExpressionSyntax initializer, SyntaxToken semicolon)
        => Create(new VariableDeclarationStatementSyntax(_syntaxTree, type, identifier, equals, initializer, semicolon));
}
