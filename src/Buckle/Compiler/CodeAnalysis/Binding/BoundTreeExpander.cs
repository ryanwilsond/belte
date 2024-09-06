using System.Collections.Generic;
using System.Collections.Immutable;
using Buckle.Diagnostics;
using static Buckle.CodeAnalysis.Binding.BoundFactory;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// Rewrites BoundStatements and all child BoundStatements, allowing expansion.
/// </summary>
internal abstract class BoundTreeExpander {
    protected static BoundStatement Simplify(List<BoundStatement> statements) {
        if (statements.Count == 1)
            return statements[0];
        else
            return Block(statements.ToArray());
    }

    protected virtual List<BoundStatement> ExpandStatement(BoundStatement statement) {
        return statement.kind switch {
            BoundNodeKind.NopStatement => ExpandNopStatement((BoundNopStatement)statement),
            BoundNodeKind.BlockStatement => ExpandBlockStatement((BoundBlockStatement)statement),
            BoundNodeKind.LocalDeclarationStatement
                => ExpandLocalDeclarationStatement((BoundLocalDeclarationStatement)statement),
            BoundNodeKind.IfStatement => ExpandIfStatement((BoundIfStatement)statement),
            BoundNodeKind.WhileStatement => ExpandWhileStatement((BoundWhileStatement)statement),
            BoundNodeKind.ForStatement => ExpandForStatement((BoundForStatement)statement),
            BoundNodeKind.ExpressionStatement => ExpandExpressionStatement((BoundExpressionStatement)statement),
            BoundNodeKind.LabelStatement => ExpandLabelStatement((BoundLabelStatement)statement),
            BoundNodeKind.GotoStatement => ExpandGotoStatement((BoundGotoStatement)statement),
            BoundNodeKind.ConditionalGotoStatement
                => ExpandConditionalGotoStatement((BoundConditionalGotoStatement)statement),
            BoundNodeKind.DoWhileStatement => ExpandDoWhileStatement((BoundDoWhileStatement)statement),
            BoundNodeKind.ReturnStatement => ExpandReturnStatement((BoundReturnStatement)statement),
            BoundNodeKind.TryStatement => ExpandTryStatement((BoundTryStatement)statement),
            BoundNodeKind.BreakStatement => ExpandBreakStatement((BoundBreakStatement)statement),
            BoundNodeKind.ContinueStatement => ExpandContinueStatement((BoundContinueStatement)statement),
            _ => throw new BelteInternalException($"ExpandStatement: unexpected expression type '{statement.kind}'"),
        };
    }

    protected virtual List<BoundStatement> ExpandNopStatement(BoundNopStatement statement) {
        return new List<BoundStatement>() { statement };
    }

    protected virtual List<BoundStatement> ExpandBlockStatement(BoundBlockStatement statement) {
        var statements = new List<BoundStatement>();

        foreach (var childStatement in statement.statements)
            statements.AddRange(ExpandStatement(childStatement));

        return new List<BoundStatement>() { Block(statements.ToArray()) };
    }

    protected virtual List<BoundStatement> ExpandLocalDeclarationStatement(
        BoundLocalDeclarationStatement statement) {
        var statements = ExpandExpression(statement.declaration.initializer, out var replacement);

        if (statements.Count > 0) {
            statements.Add(new BoundLocalDeclarationStatement(
                new BoundVariableDeclaration(statement.declaration.variable, replacement)
            ));

            return statements;
        }

        return new List<BoundStatement>() { statement };
    }

    protected virtual List<BoundStatement> ExpandIfStatement(BoundIfStatement statement) {
        var statements = ExpandExpression(statement.condition, out var conditionReplacement);

        statements.Add(
            new BoundIfStatement(
                conditionReplacement,
                Simplify(ExpandStatement(statement.then)),
                statement.elseStatement != null ? Simplify(ExpandStatement(statement.elseStatement)) : null
            )
        );

        return statements;
    }

    protected virtual List<BoundStatement> ExpandWhileStatement(BoundWhileStatement statement) {
        var statements = ExpandExpression(statement.condition, out var conditionReplacement);

        statements.Add(
            new BoundWhileStatement(
                conditionReplacement,
                Simplify(ExpandStatement(statement.body)),
                statement.breakLabel,
                statement.continueLabel
            )
        );

        return statements;
    }

    protected virtual List<BoundStatement> ExpandForStatement(BoundForStatement statement) {
        // For loops have to be expanded after they have been lowered
        return new List<BoundStatement>() { statement };
    }

    protected virtual List<BoundStatement> ExpandExpressionStatement(BoundExpressionStatement statement) {
        var statements = ExpandExpression(statement.expression, out var replacement);

        if (statements.Count != 0) {
            statements.Add(new BoundExpressionStatement(replacement));
            return statements;
        }

        return new List<BoundStatement>() { statement };
    }

    protected virtual List<BoundStatement> ExpandLabelStatement(BoundLabelStatement statement) {
        return new List<BoundStatement>() { statement };
    }

    protected virtual List<BoundStatement> ExpandGotoStatement(BoundGotoStatement statement) {
        return new List<BoundStatement>() { statement };
    }

    protected virtual List<BoundStatement> ExpandConditionalGotoStatement(BoundConditionalGotoStatement statement) {
        return new List<BoundStatement>() { statement };
    }

    protected virtual List<BoundStatement> ExpandDoWhileStatement(BoundDoWhileStatement statement) {
        var statements = ExpandExpression(statement.condition, out var conditionReplacement);

        statements.Add(
            new BoundDoWhileStatement(
                Simplify(ExpandStatement(statement.body)),
                conditionReplacement,
                statement.breakLabel,
                statement.continueLabel
            )
        );

        return statements;
    }

    protected virtual List<BoundStatement> ExpandReturnStatement(BoundReturnStatement statement) {
        if (statement.expression is null)
            return new List<BoundStatement>() { statement };

        var statements = ExpandExpression(statement.expression, out var replacement);

        if (statements.Count != 0) {
            statements.Add(new BoundReturnStatement(replacement));
            return statements;
        }

        return new List<BoundStatement>() { statement };
    }

    protected virtual List<BoundStatement> ExpandTryStatement(BoundTryStatement statement) {
        return new List<BoundStatement>() {
            new BoundTryStatement(
                Simplify(ExpandStatement(statement.body)) as BoundBlockStatement,
                statement.catchBody != null ?
                    Simplify(ExpandStatement(statement.catchBody)) as BoundBlockStatement
                    : null,
                statement.finallyBody != null ?
                    Simplify(ExpandStatement(statement.finallyBody)) as BoundBlockStatement
                    : null
            )
        };
    }

    protected virtual List<BoundStatement> ExpandBreakStatement(BoundBreakStatement statement) {
        return new List<BoundStatement>() { statement };
    }

    protected virtual List<BoundStatement> ExpandContinueStatement(BoundContinueStatement statement) {
        return new List<BoundStatement>() { statement };
    }

    protected virtual List<BoundStatement> ExpandExpression(
        BoundExpression expression,
        out BoundExpression replacement) {
        switch (expression.kind) {
            case BoundNodeKind.BinaryExpression:
                return ExpandBinaryExpression((BoundBinaryExpression)expression, out replacement);
            case BoundNodeKind.LiteralExpression:
                if (expression is BoundInitializerListExpression il)
                    return ExpandInitializerListExpression(il, out replacement);
                return ExpandLiteralExpression((BoundLiteralExpression)expression, out replacement);
            case BoundNodeKind.VariableExpression:
                return ExpandVariableExpression((BoundVariableExpression)expression, out replacement);
            case BoundNodeKind.AssignmentExpression:
                return ExpandAssignmentExpression((BoundAssignmentExpression)expression, out replacement);
            case BoundNodeKind.UnaryExpression:
                return ExpandUnaryExpression((BoundUnaryExpression)expression, out replacement);
            case BoundNodeKind.EmptyExpression:
                return ExpandEmptyExpression((BoundEmptyExpression)expression, out replacement);
            case BoundNodeKind.ErrorExpression:
                return ExpandErrorExpression((BoundErrorExpression)expression, out replacement);
            case BoundNodeKind.CallExpression:
                return ExpandCallExpression((BoundCallExpression)expression, out replacement);
            case BoundNodeKind.CastExpression:
                return ExpandCastExpression((BoundCastExpression)expression, out replacement);
            case BoundNodeKind.IndexExpression:
                return ExpandIndexExpression((BoundIndexExpression)expression, out replacement);
            case BoundNodeKind.CompoundAssignmentExpression:
                return ExpandCompoundAssignmentExpression(
                    (BoundCompoundAssignmentExpression)expression, out replacement
                );
            case BoundNodeKind.ReferenceExpression:
                return ExpandReferenceExpression((BoundReferenceExpression)expression, out replacement);
            case BoundNodeKind.TypeOfExpression:
                return ExpandTypeOfExpression((BoundTypeOfExpression)expression, out replacement);
            case BoundNodeKind.TernaryExpression:
                return ExpandTernaryExpression((BoundTernaryExpression)expression, out replacement);
            case BoundNodeKind.ObjectCreationExpression:
                return ExpandObjectCreationExpression((BoundObjectCreationExpression)expression, out replacement);
            case BoundNodeKind.MemberAccessExpression:
                return ExpandMemberAccessExpression((BoundMemberAccessExpression)expression, out replacement);
            case BoundNodeKind.PrefixExpression:
                return ExpandPrefixExpression((BoundPrefixExpression)expression, out replacement);
            case BoundNodeKind.PostfixExpression:
                return ExpandPostfixExpression((BoundPostfixExpression)expression, out replacement);
            case BoundNodeKind.TypeWrapper:
                return ExpandTypeWrapper((BoundTypeWrapper)expression, out replacement);
            case BoundNodeKind.ThisExpression:
                return ExpandThisExpression((BoundThisExpression)expression, out replacement);
            case BoundNodeKind.BaseExpression:
                return ExpandBaseExpression((BoundBaseExpression)expression, out replacement);
            case BoundNodeKind.ThrowExpression:
                return ExpandThrowExpression((BoundThrowExpression)expression, out replacement);
            case BoundNodeKind.Type:
                return ExpandType((BoundType)expression, out replacement);
            default:
                throw new BelteInternalException($"ExpandExpression: unexpected expression type '{expression.kind}'");
        }
    }

    protected virtual List<BoundStatement> ExpandType(BoundType expression, out BoundExpression replacement) {
        replacement = expression;
        return new List<BoundStatement>() { };
    }

    protected virtual List<BoundStatement> ExpandThisExpression(
        BoundThisExpression expression,
        out BoundExpression replacement) {
        replacement = expression;
        return new List<BoundStatement>() { };
    }

    protected virtual List<BoundStatement> ExpandBaseExpression(
        BoundBaseExpression expression,
        out BoundExpression replacement) {
        replacement = expression;
        return new List<BoundStatement>() { };
    }

    protected virtual List<BoundStatement> ExpandThrowExpression(
        BoundThrowExpression expression,
        out BoundExpression replacement) {
        replacement = expression;
        return new List<BoundStatement>() { };
    }

    protected virtual List<BoundStatement> ExpandBinaryExpression(
        BoundBinaryExpression expression, out BoundExpression replacement) {
        var statements = ExpandExpression(expression.left, out var leftReplacement);
        statements.AddRange(ExpandExpression(expression.right, out var rightReplacement));

        if (statements.Count != 0) {
            replacement = new BoundBinaryExpression(leftReplacement, expression.op, rightReplacement);
            return statements;
        }

        replacement = expression;
        return new List<BoundStatement>() { };
    }

    protected virtual List<BoundStatement> ExpandInitializerListExpression(
        BoundInitializerListExpression expression, out BoundExpression replacement) {
        var statements = new List<BoundStatement>();
        var replacementItems = ImmutableArray.CreateBuilder<BoundExpression>();

        foreach (var item in expression.items) {
            statements.AddRange(ExpandExpression(item, out var itemReplacement));
            replacementItems.Add(itemReplacement);
        }

        if (statements.Count != 0) {
            replacement = new BoundInitializerListExpression(replacementItems.ToImmutable(), expression.type);
            return statements;
        }

        replacement = expression;
        return new List<BoundStatement>() { };
    }

    protected virtual List<BoundStatement> ExpandLiteralExpression(
        BoundLiteralExpression expression, out BoundExpression replacement) {
        replacement = expression;
        return new List<BoundStatement>() { };
    }

    protected virtual List<BoundStatement> ExpandVariableExpression(
        BoundVariableExpression expression, out BoundExpression replacement) {
        replacement = expression;
        return new List<BoundStatement>() { };
    }

    protected virtual List<BoundStatement> ExpandAssignmentExpression(
        BoundAssignmentExpression expression, out BoundExpression replacement) {
        var statements = ExpandExpression(expression.left, out var leftReplacement);
        statements.AddRange(ExpandExpression(expression.right, out var rightReplacement));

        if (statements.Count != 0) {
            replacement = new BoundAssignmentExpression(leftReplacement, rightReplacement);
            return statements;
        }

        replacement = expression;
        return new List<BoundStatement>() { };
    }

    protected virtual List<BoundStatement> ExpandUnaryExpression(
        BoundUnaryExpression expression, out BoundExpression replacement) {
        var statements = ExpandExpression(expression.operand, out var operandReplacement);

        if (statements.Count != 0) {
            replacement = new BoundUnaryExpression(expression.op, operandReplacement);
            return statements;
        }

        replacement = expression;
        return new List<BoundStatement>() { };
    }

    protected virtual List<BoundStatement> ExpandEmptyExpression(
        BoundEmptyExpression expression, out BoundExpression replacement) {
        replacement = expression;
        return new List<BoundStatement>() { };
    }

    protected virtual List<BoundStatement> ExpandErrorExpression(
        BoundErrorExpression expression, out BoundExpression replacement) {
        replacement = expression;
        return new List<BoundStatement>() { };
    }

    protected virtual List<BoundStatement> ExpandCallExpression(
        BoundCallExpression expression,
        out BoundExpression replacement) {
        var statements = ExpandExpression(expression.expression, out var expressionReplacement);
        var replacementArguments = ImmutableArray.CreateBuilder<BoundExpression>();

        foreach (var argument in expression.arguments) {
            statements.AddRange(ExpandExpression(argument, out var argumentReplacement));
            replacementArguments.Add(argumentReplacement);
        }

        replacement = new BoundCallExpression(
            expressionReplacement,
            expression.method,
            replacementArguments.ToImmutable(),
            expression.templateArguments
        );

        return statements;
    }

    protected virtual List<BoundStatement> ExpandCastExpression(
        BoundCastExpression expression,
        out BoundExpression replacement) {
        var statements = ExpandExpression(expression.expression, out var expressionReplacement);

        if (statements.Count != 0) {
            replacement = new BoundCastExpression(expression.type, expressionReplacement);
            return statements;
        }

        replacement = expression;
        return new List<BoundStatement>() { };
    }

    protected virtual List<BoundStatement> ExpandIndexExpression(
        BoundIndexExpression expression, out BoundExpression replacement) {
        var statements = ExpandExpression(expression.expression, out var operandReplacement);
        statements.AddRange(ExpandExpression(expression.index, out var indexReplacement));

        if (statements.Count != 0) {
            replacement = new BoundIndexExpression(operandReplacement, indexReplacement, expression.isNullConditional);
            return statements;
        }

        replacement = expression;
        return new List<BoundStatement>() { };
    }

    protected virtual List<BoundStatement> ExpandCompoundAssignmentExpression(
        BoundCompoundAssignmentExpression expression, out BoundExpression replacement) {
        var statements = ExpandExpression(expression.left, out var leftReplacement);
        statements.AddRange(ExpandExpression(expression.right, out var rightReplacement));

        if (statements.Count != 0) {
            replacement = new BoundCompoundAssignmentExpression(leftReplacement, expression.op, rightReplacement);
            return statements;
        }

        replacement = expression;
        return new List<BoundStatement>() { };
    }

    protected virtual List<BoundStatement> ExpandReferenceExpression(
        BoundReferenceExpression expression, out BoundExpression replacement) {
        replacement = expression;
        return new List<BoundStatement>() { };
    }

    protected virtual List<BoundStatement> ExpandTypeOfExpression(
        BoundTypeOfExpression expression, out BoundExpression replacement) {
        replacement = expression;
        return new List<BoundStatement>() { };
    }

    protected virtual List<BoundStatement> ExpandTernaryExpression(
        BoundTernaryExpression expression, out BoundExpression replacement) {
        var statements = ExpandExpression(expression.left, out var leftReplacement);
        statements.AddRange(ExpandExpression(expression.center, out var centerReplacement));
        statements.AddRange(ExpandExpression(expression.right, out var rightReplacement));

        if (statements.Count != 0) {
            replacement = new BoundTernaryExpression(
                leftReplacement, expression.op, centerReplacement, rightReplacement
            );

            return statements;
        }

        replacement = expression;
        return new List<BoundStatement>() { };
    }

    protected virtual List<BoundStatement> ExpandObjectCreationExpression(
        BoundObjectCreationExpression expression, out BoundExpression replacement) {
        replacement = expression;
        return new List<BoundStatement>() { };
    }

    protected virtual List<BoundStatement> ExpandMemberAccessExpression(
        BoundMemberAccessExpression expression, out BoundExpression replacement) {
        var statements = ExpandExpression(expression.left, out var leftReplacement);
        statements.AddRange(ExpandExpression(expression.right, out var rightReplacement));

        if (statements.Count != 0) {
            replacement = new BoundMemberAccessExpression(
                leftReplacement,
                rightReplacement,
                expression.isNullConditional,
                expression.isStaticAccess
            );

            return statements;
        }

        replacement = expression;
        return new List<BoundStatement>() { };
    }

    protected virtual List<BoundStatement> ExpandPrefixExpression(
        BoundPrefixExpression expression, out BoundExpression replacement) {
        var statements = ExpandExpression(expression.operand, out var operandReplacement);

        if (statements.Count != 0) {
            replacement = new BoundPrefixExpression(expression.op, operandReplacement);
            return statements;
        }

        replacement = expression;
        return new List<BoundStatement>() { };
    }

    protected virtual List<BoundStatement> ExpandPostfixExpression(
        BoundPostfixExpression expression, out BoundExpression replacement) {
        var statements = ExpandExpression(expression.operand, out var operandReplacement);

        if (statements.Count != 0) {
            replacement = new BoundPostfixExpression(operandReplacement, expression.op, expression.isOwnStatement);
            return statements;
        }

        replacement = expression;
        return new List<BoundStatement>() { };
    }

    protected virtual List<BoundStatement> ExpandTypeWrapper(
        BoundTypeWrapper expression,
        out BoundExpression replacement) {
        replacement = expression;
        return new List<BoundStatement>() { };
    }
}
