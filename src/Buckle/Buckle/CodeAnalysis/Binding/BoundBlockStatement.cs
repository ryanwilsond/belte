using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound block statement, bound from a parser BlockStatement.
/// </summary>
internal sealed class BoundBlockStatement : BoundStatement {
    internal BoundBlockStatement(ImmutableArray<BoundStatement> statements) {
        this.statements = statements;
    }

    internal ImmutableArray<BoundStatement> statements { get; }

    internal override BoundNodeType type => BoundNodeType.BlockStatement;
}
