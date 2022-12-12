using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Syntax;

// TODO Make sure that block statements can still have return statements (they might)
/// <summary>
/// Inline function expression, similar to local function but is evaluated immediately and has no signature.
/// E.g. { ... statements (including a return statement) ... }
/// The only thing distinguishing an inline function expression from a block statement is a return statement.
/// </summary>
internal sealed partial class InlineFunctionExpression : Expression {
    /// <param name="statements">Contains at least one return statement</param>
    internal InlineFunctionExpression(
        SyntaxTree syntaxTree, Token openBrace, ImmutableArray<Statement> statements, Token closeBrace)
        : base(syntaxTree) {
        this.openBrace = openBrace;
        this.statements = statements;
        this.closeBrace = closeBrace;
    }

    internal Token? openBrace { get; }

    /// <summary>
    /// Contains at least one return statement.
    /// </summary>
    internal ImmutableArray<Statement> statements { get; }

    internal Token? closeBrace { get; }

    internal override SyntaxType type => SyntaxType.InlineFunction;
}
