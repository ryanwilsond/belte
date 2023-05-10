
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// A global statement, just a subset of statements that are allowed in the global scope.
/// </summary>
internal sealed partial class GlobalStatementSyntax : MemberSyntax {
    /// <summary>
    /// Creates a <see cref="GlobalStatementSyntax" />.
    /// </summary>
    /// <param name="syntaxTree"><see cref="SyntaxTree" /> this <see cref="SyntaxNode" /> resides in.</param>
    /// <param name="statement"><see cref="StatementSyntax" />.</param>
    /// <returns>.</returns>
    internal GlobalStatementSyntax(SyntaxTree syntaxTree, StatementSyntax statement) : base(syntaxTree) {
        this.statement = statement;
    }

    public override SyntaxKind kind => SyntaxKind.GlobalStatement;

    /// <summary>
    /// <see cref="StatementSyntax" /> (should ignore that fact that it is global).
    /// </summary>
    internal StatementSyntax statement { get; }
}

internal sealed partial class SyntaxFactory {
    internal GlobalStatementSyntax GlobalStatement(StatementSyntax statement)
        => Create(new GlobalStatementSyntax(_syntaxTree, statement));
}
