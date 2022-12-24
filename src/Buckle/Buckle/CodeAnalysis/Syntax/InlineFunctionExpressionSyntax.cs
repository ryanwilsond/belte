using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Inline function expression, similar to local function but is evaluated immediately and has no signature.<br/>
/// E.g.
/// <code>
/// {
///     ... statements (including a return statement) ...
/// }
/// </code>
/// The only thing distinguishing an <see cref="InlineFunctionExpressionSyntax" /> from a
/// <see cref="BlockStatementSyntax" /> is a <see cref="ReturnStatementSyntax" />.
/// </summary>
internal sealed partial class InlineFunctionExpressionSyntax : ExpressionSyntax {
    /// <param name="statements">Contains at least one <see cref="ReturnStatementSyntax" />.</param>
    internal InlineFunctionExpressionSyntax(
        SyntaxTree syntaxTree, SyntaxToken openBrace,
        ImmutableArray<StatementSyntax> statements, SyntaxToken closeBrace)
        : base(syntaxTree) {
        this.openBrace = openBrace;
        this.statements = statements;
        this.closeBrace = closeBrace;
    }

    internal SyntaxToken? openBrace { get; }

    /// <summary>
    /// Contains at least one <see cref="ReturnStatementSyntax" />.
    /// </summary>
    internal ImmutableArray<StatementSyntax> statements { get; }

    internal SyntaxToken? closeBrace { get; }

    internal override SyntaxKind kind => SyntaxKind.InlineFunction;
}
