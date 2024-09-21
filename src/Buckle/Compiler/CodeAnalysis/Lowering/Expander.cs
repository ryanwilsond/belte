using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Libraries.Standard;

namespace Buckle.CodeAnalysis.Lowering;

/// <summary>
/// Expands expressions to make them simpler to handle by the <see cref="Lowerer" />.
/// </summary>
internal sealed class Expander : BoundTreeExpander {
    private readonly List<string> _localNames = new List<string>();

    private int _tempCount = 0;
    private int _compoundAssignmentDepth = 0;
    private int _operatorDepth = 0;

    internal bool transpilerMode = false;

    /// <summary>
    /// Expands all expression in a <see cref="BoundStatement" />.
    /// If <param name="statement" /> is not a <see cref="BoundBlockStatement" /> and any expansion occurs, a
    /// <see cref="BoundBlockStatement" /> will be returned.
    /// </summary>
    /// <param name="statement"><see cref="BoundStatement" /> to expand expressions in.</param>
    /// <returns>Expanded <param name="statement" />.</returns>
    internal BoundStatement Expand(BoundStatement statement) {
        return Simplify(ExpandStatement(statement));
    }

    protected override List<BoundStatement> ExpandLocalDeclarationStatement(
        BoundLocalDeclarationStatement statement) {
        _localNames.Add(statement.declaration.variable.name);
        return base.ExpandLocalDeclarationStatement(statement);
    }

    protected override List<BoundStatement> ExpandCompoundAssignmentExpression(
        BoundCompoundAssignmentExpression expression,
        out BoundExpression replacement) {
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
            var statements = ExpandCallExpressionInternal(expression, out var callReplacement);
            var tempLocal = GenerateTempLocal(expression.type);

            statements.Add(new BoundLocalDeclarationStatement(
                new BoundVariableDeclaration(tempLocal, callReplacement)
            ));

            replacement = new BoundVariableExpression(tempLocal);

            return statements;
        }

        return ExpandCallExpressionInternal(expression, out replacement);
    }

    private List<BoundStatement> ExpandCallExpressionInternal(
        BoundCallExpression expression,
        out BoundExpression replacement) {
        if (transpilerMode && expression.method.containingType == StandardLibrary.Math) {
            var statements = ExpandExpression(expression.expression, out var expressionReplacement);
            var replacementArguments = ImmutableArray.CreateBuilder<BoundExpression>();

            foreach (var argument in expression.arguments) {
                var tempLocal = GenerateTempLocal(argument.type);
                statements.AddRange(ExpandExpression(argument, out var argumentReplacement));
                statements.Add(new BoundLocalDeclarationStatement(
                    new BoundVariableDeclaration(tempLocal, argumentReplacement)
                ));

                replacementArguments.Add(new BoundVariableExpression(tempLocal));
            }

            replacement = new BoundCallExpression(
                expressionReplacement,
                expression.method,
                replacementArguments.ToImmutable(),
                expression.templateArguments
            );

            return statements;
        }

        var baseStatements = base.ExpandCallExpression(expression, out replacement);
        return baseStatements;
    }

    protected override List<BoundStatement> ExpandBinaryExpression(
        BoundBinaryExpression expression,
        out BoundExpression replacement) {
        _operatorDepth++;

        if (_operatorDepth > 1) {
            var statements = ExpandExpression(expression.left, out var leftReplacement);
            statements.AddRange(ExpandExpression(expression.right, out var rightReplacement));

            var tempLocal = GenerateTempLocal(expression.type);

            statements.Add(
                new BoundLocalDeclarationStatement(new BoundVariableDeclaration(
                    tempLocal,
                    new BoundBinaryExpression(leftReplacement, expression.op, rightReplacement)
                ))
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
        BoundTernaryExpression expression,
        out BoundExpression replacement) {
        _operatorDepth++;

        if (_operatorDepth > 1) {
            var statements = ExpandExpression(expression.left, out var leftReplacement);
            statements.AddRange(ExpandExpression(expression.center, out var centerReplacement));
            statements.AddRange(ExpandExpression(expression.right, out var rightReplacement));

            var tempLocal = GenerateTempLocal(expression.type);

            statements.Add(
                new BoundLocalDeclarationStatement(new BoundVariableDeclaration(
                    tempLocal,
                    new BoundTernaryExpression(leftReplacement, expression.op, centerReplacement, rightReplacement)
                ))
            );

            replacement = new BoundVariableExpression(tempLocal);
            _operatorDepth--;
            return statements;
        }

        var baseStatements = base.ExpandTernaryExpression(expression, out replacement);
        _operatorDepth--;
        return baseStatements;
    }

    protected override List<BoundStatement> ExpandInitializerDictionaryExpression(
        BoundInitializerDictionaryExpression expression,
        out BoundExpression replacement) {
        // TODO Add a way where if _operatorDepth == 0 a temp local isn't made if this is a variable initializer
        var dictionaryType = expression.type.typeSymbol as NamedTypeSymbol;
        var tempLocal = GenerateTempLocal(expression.type);
        var statements = new List<BoundStatement>() {
            new BoundLocalDeclarationStatement(new BoundVariableDeclaration(
                tempLocal,
                new BoundObjectCreationExpression(
                    expression.type,
                    dictionaryType.constructors[0],
                    []
                )
            ))
        };

        foreach (var pair in expression.items) {
            statements.Add(new BoundExpressionStatement(new BoundCallExpression(
                new BoundVariableExpression(tempLocal),
                dictionaryType.GetMembers("Add").Single() as MethodSymbol,
                [pair.Item1, pair.Item2],
                []
            )));
        }

        replacement = new BoundVariableExpression(tempLocal);
        return statements;
    }

    private LocalVariableSymbol GenerateTempLocal(BoundType type) {
        string name;
        do {
            name = $"temp{_tempCount++}";
        } while (_localNames.Contains(name));

        return new LocalVariableSymbol(name, type, null, DeclarationModifiers.None);
    }
}
