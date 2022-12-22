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
/// The only thing distinguishing an <see cref="InlineFunctionExpression" /> from a <see cref="BlockStatement" /> is a
/// <see cref="ReturnStatement" />.
/// </summary>
internal sealed partial class InlineFunctionExpression : Expression {
    /// <param name="statements">Contains at least one <see cref="ReturnStatement" />.</param>
    internal InlineFunctionExpression(
        SyntaxTree syntaxTree, Token openBrace, ImmutableArray<Statement> statements, Token closeBrace)
        : base(syntaxTree) {
        this.openBrace = openBrace;
        this.statements = statements;
        this.closeBrace = closeBrace;
    }

    internal Token? openBrace { get; }

    /// <summary>
    /// Contains at least one <see cref="ReturnStatement" />.
    /// </summary>
    internal ImmutableArray<Statement> statements { get; }

    internal Token? closeBrace { get; }

    internal override SyntaxType type => SyntaxType.InlineFunction;
}
