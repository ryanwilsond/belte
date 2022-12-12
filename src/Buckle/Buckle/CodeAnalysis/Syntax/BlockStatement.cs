using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Block statement, group of statements enclosed by curly braces.
/// The child statements have their own local scope.
/// E.g.
/// {
///     ... statements ...
/// }
/// </summary>
internal sealed partial class BlockStatement : Statement {
    /// <param name="statements">Child statements</param>
    internal BlockStatement(
        SyntaxTree syntaxTree, Token openBrace, ImmutableArray<Statement> statements, Token closeBrace)
        : base(syntaxTree) {
        this.openBrace = openBrace;
        this.statements = statements;
        this.closeBrace = closeBrace;
    }

    internal Token openBrace { get; }

    internal ImmutableArray<Statement> statements { get; }

    internal Token closeBrace { get; }

    internal override SyntaxType type => SyntaxType.Block;
}
