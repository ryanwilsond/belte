using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Block statement, group of statements enclosed by curly braces.
/// The child statements have their own local scope.<br/>
/// E.g.
/// <code>
/// {
///     ... statements ...
/// }
/// </code>
/// </summary>
internal sealed partial class BlockStatementSyntax : StatementSyntax {
    /// <param name="statements">Child statements.</param>
    internal BlockStatementSyntax(
        SyntaxTree syntaxTree, SyntaxToken openBrace,
        ImmutableArray<StatementSyntax> statements, SyntaxToken closeBrace)
        : base(syntaxTree) {
        this.openBrace = openBrace;
        this.statements = statements;
        this.closeBrace = closeBrace;
    }

    public override SyntaxKind kind => SyntaxKind.BlockStatement;

    internal SyntaxToken openBrace { get; }

    internal ImmutableArray<StatementSyntax> statements { get; }

    internal SyntaxToken closeBrace { get; }
}

internal sealed partial class SyntaxFactory {
    internal BlockStatementSyntax BlockStatement(
        SyntaxToken openBrace, ImmutableArray<StatementSyntax> statements, SyntaxToken closeBrace)
        => Create(new BlockStatementSyntax(_syntaxTree, openBrace, statements, closeBrace));
}
