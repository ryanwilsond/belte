
namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound try statement, bound from a <see cref="Syntax.TryStatementSyntax" />.
/// Instead of having a <see cref="Syntax.CatchClauseSyntax" /> and <see cref="Syntax.FinallyClauseSyntax" />,
/// it just has their bodies.
/// </summary>
internal sealed class BoundTryStatement : BoundStatement {
    internal BoundTryStatement(
        BoundBlockStatement body, BoundBlockStatement catchBody, BoundBlockStatement finallyBody) {
        this.body = body;
        this.catchBody = catchBody;
        this.finallyBody = finallyBody;
    }

    internal BoundBlockStatement body { get; }

    internal BoundBlockStatement catchBody { get; }

    internal BoundBlockStatement finallyBody { get; }

    internal override BoundNodeKind kind => BoundNodeKind.TryStatement;
}
