using System.Collections.Generic;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Lowering;

/// <summary>
/// Expands expressions to make them simpler to handle by the <see cref="Lowerer" />.
/// </summary>
internal sealed class Expander : BoundTreeExpander {
    private readonly List<string> _localNames = new List<string>();

    private int _tempCount = 0;
    private int _compoundAssignmentDepth = 0;
    private int _operatorDepth = 0;

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

    protected override List<BoundStatement> ExpandVariableDeclarationStatement(
        BoundLocalDeclarationStatement statement) {
        _localNames.Add(statement.variable.name);
        return base.ExpandVariableDeclarationStatement(statement);
    }

    protected override List<BoundStatement> ExpandCompoundAssignmentExpression(
        BoundCompoundAssignmentExpression expression, out BoundExpression replacement) {
        _compoundAssignmentDepth++;

        if (_compoundAssignmentDepth > 1) {
            var statements = ExpandExpression(expression.left, out var leftReplacement);
            statements.AddRange(ExpandExpression(expression.right, out var rightReplacement));

            statements.Add(
                new BoundExpressionStatement(
                    new BoundCompoundAssignmentExpression(leftReplacement, expression.op, rightReplacement)
                )
            );

            replacement = leftReplacement;
            _compoundAssignmentDepth--;
            return statements;
        }

        var baseStatements = base.ExpandCompoundAssignmentExpression(expression, out replacement);
        _compoundAssignmentDepth--;
        return baseStatements;
    }

    protected override List<BoundStatement> ExpandCallExpression(
        BoundCallExpression expression,
        out BoundExpression replacement) {
        if (_operatorDepth > 0) {
            var statements = base.ExpandCallExpression(expression, out var callReplacement);
            var tempLocal = GenerateTempLocal(expression.type);

            statements.Add(new BoundLocalDeclarationStatement(tempLocal, callReplacement));
            replacement = new BoundVariableExpression(tempLocal);

            return statements;
        }

        var baseStatements = base.ExpandCallExpression(expression, out replacement);
        return baseStatements;
    }

    protected override List<BoundStatement> ExpandBinaryExpression(
        BoundBinaryExpression expression, out BoundExpression replacement) {
        _operatorDepth++;

        if (_operatorDepth > 1) {
            var statements = ExpandExpression(expression.left, out var leftReplacement);
            statements.AddRange(ExpandExpression(expression.right, out var rightReplacement));

            var tempLocal = GenerateTempLocal(expression.type);

            statements.Add(
                new BoundLocalDeclarationStatement(
                    tempLocal,
                    new BoundBinaryExpression(leftReplacement, expression.op, rightReplacement)
                )
            );

            replacement = new BoundVariableExpression(tempLocal);
            _operatorDepth--;
            return statements;
        }

        var baseStatements = base.ExpandBinaryExpression(expression, out replacement);
        _operatorDepth--;
        return baseStatements;
    }

    protected override List<BoundStatement> ExpandTernaryExpression(
        BoundTernaryExpression expression, out BoundExpression replacement) {
        _operatorDepth++;

        if (_operatorDepth > 1) {
            var statements = ExpandExpression(expression.left, out var leftReplacement);
            statements.AddRange(ExpandExpression(expression.center, out var centerReplacement));
            statements.AddRange(ExpandExpression(expression.right, out var rightReplacement));

            var tempLocal = GenerateTempLocal(expression.type);

            statements.Add(
                new BoundLocalDeclarationStatement(
                    tempLocal,
                    new BoundTernaryExpression(leftReplacement, expression.op, centerReplacement, rightReplacement)
                )
            );

            replacement = new BoundVariableExpression(tempLocal);
            _operatorDepth--;
            return statements;
        }

        var baseStatements = base.ExpandTernaryExpression(expression, out replacement);
        _operatorDepth--;
        return baseStatements;
    }

    private LocalVariableSymbol GenerateTempLocal(BoundType type) {
        string name;
        do {
            name = $"temp{_tempCount++}";
        } while (_localNames.Contains(name));

        return new LocalVariableSymbol(name, type, null);
    }
}
