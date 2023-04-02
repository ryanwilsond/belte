using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Empty expression, used as debugging expressions and placeholders.
/// Can only be created in a source file by creating an <see cref="ExpressionStatementSyntax" /> with an
/// <see cref="EmptyExpressionSyntax" />:<br/>
/// <code>
///     ;
/// </code>
/// </summary>
internal sealed partial class EmptyExpressionSyntax : ExpressionSyntax {
    /// <param name="location">An artificial location used by diagnostics.</param>
    internal EmptyExpressionSyntax(SyntaxTree syntaxTree, SyntaxToken artificialLocation = null) : base(syntaxTree) {
        this.artificialLocation = artificialLocation;
    }

    /// <summary>
    /// An artificial location used by diagnostics.
    /// </summary>
    SyntaxToken? artificialLocation { get; }

    internal override SyntaxKind kind => SyntaxKind.EmptyExpression;
}

internal sealed partial class SyntaxFactory {
    internal EmptyExpressionSyntax Empty(SyntaxToken artificialLocation = null) =>
        Create(new EmptyExpressionSyntax(_syntaxTree, artificialLocation));
}
