using System.Collections.Generic;
using Buckle.CodeAnalysis.Binding;

namespace Buckle.CodeAnalysis.Lowering;

/// <summary>
/// Expands expressions to make them simpler to handle by the <see cref="Lowerer" />.
/// </summary>
internal sealed class Expander : BoundTreeExpander {
    private int _tempCount;

    /// <summary>
    /// Expands all expression in a <see cref="BoundStatement" />.
    /// If <param name="statement" /> is not a <see cref="BoundBlockStatement" /> and any expansion occurs, a
    /// <see cref="BoundBlockStatement" /> will be returned.
    /// </summary>
    /// <param name="statement"><see cref="BoundStatement" /> to expand expressions in.</param>
    /// <returns>Expanded <param name="statement" />.</returns>
    internal static BoundStatement Expand(BoundStatement statement) {
        return Simplify(ExpandAsList(statement));
    }

    /// <summary>
    /// Expands all expression in a <see cref="BoundStatement" />.
    /// </summary>
    /// <param name="statement"><see cref="BoundStatement" /> to expand expressions in.</param>
    /// <returns>List of expanded statements.</returns>
    internal static List<BoundStatement> ExpandAsList(BoundStatement statement) {
        var expander = new Expander();
        var statements = expander.ExpandStatement(statement);
        return statements;
    }

    protected override List<BoundStatement> ExpandCompoundAssignmentExpression(
        BoundCompoundAssignmentExpression expression, out BoundExpression replacement) {
        // ! TEMP - This should actually expand something
        return base.ExpandCompoundAssignmentExpression(expression, out replacement);
    }
}