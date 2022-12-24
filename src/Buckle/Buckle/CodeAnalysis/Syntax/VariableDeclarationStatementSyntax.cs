
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Variable declaration, definition is optional.<br/>
/// E.g.
/// <code>
/// int myVar = 3;
/// </code>
/// </summary>
internal sealed partial class VariableDeclarationStatementSyntax : StatementSyntax {
    /// <param name="typeClause"><see cref="TypeClauseSyntax" /> of the variable being declared.</param>
    /// <param name="identifier">Name of the variable.</param>
    /// <param name="equals">Equals <see cref="SyntaxToken" /> (optional).</param>
    /// <param name="initializer">Definition value (optional).</param>
    internal VariableDeclarationStatementSyntax(
        SyntaxTree syntaxTree, TypeClauseSyntax typeClause,
        SyntaxToken identifier, SyntaxToken equals, ExpressionSyntax initializer, SyntaxToken semicolon)
        : base(syntaxTree) {
        this.typeClause = typeClause;
        this.identifier = identifier;
        this.equals = equals;
        this.initializer = initializer;
        this.semicolon = semicolon;
    }

    /// <summary>
    /// <see cref="TypeClauseSyntax" /> of the variable being declared.
    /// </summary>
    internal TypeClauseSyntax typeClause { get; }

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

    internal override SyntaxKind kind => SyntaxKind.VariableDeclarationStatement;
}
