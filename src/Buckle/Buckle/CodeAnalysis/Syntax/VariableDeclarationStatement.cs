
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Variable declaration, definition is optional.<br/>
/// E.g.
/// <code>
/// int myVar = 3;
/// </code>
/// </summary>
internal sealed partial class VariableDeclarationStatement : Statement {
    /// <param name="typeClause"><see cref="TypeClause" /> of the variable being declared.</param>
    /// <param name="identifier">Name of the variable.</param>
    /// <param name="equals">Equals <see cref="Token" /> (optional).</param>
    /// <param name="initializer">Definition value (optional).</param>
    internal VariableDeclarationStatement(
        SyntaxTree syntaxTree, TypeClause typeClause,
        Token identifier, Token equals, Expression initializer, Token semicolon)
        : base(syntaxTree) {
        this.typeClause = typeClause;
        this.identifier = identifier;
        this.equals = equals;
        this.initializer = initializer;
        this.semicolon = semicolon;
    }

    /// <summary>
    /// <see cref="TypeClause" /> of the variable being declared.
    /// </summary>
    internal TypeClause typeClause { get; }

    /// <summary>
    /// Name of the variable.
    /// </summary>
    internal Token identifier { get; }

    /// <summary>
    /// Equals <see cref="Token" /> (optional).
    /// </summary>
    internal Token? equals { get; }

    /// <summary>
    /// Definition value (optional).
    /// </summary>
    internal Expression? initializer { get; }

    internal Token semicolon { get; }

    internal override SyntaxType type => SyntaxType.VariableDeclarationStatement;
}
