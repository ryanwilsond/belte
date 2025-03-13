using System.Collections.Generic;
using System.Collections.Immutable;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;
using Microsoft.CodeAnalysis.PooledObjects;
using static Buckle.CodeAnalysis.Binding.BoundFactory;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// Rewrites BoundStatements and all child BoundStatements, allowing expansion.
/// </summary>
internal abstract class BoundTreeExpander {
    private protected static BoundStatement Simplify(SyntaxNode syntax, List<BoundStatement> statements) {
        if (statements.Count == 1)
            return statements[0];
        else
            return Block(syntax, statements.ToArray());
    }

    private protected virtual List<BoundStatement> ExpandStatement(BoundStatement statement) {
        return statement.kind switch {
            BoundKind.NopStatement => ExpandNopStatement((BoundNopStatement)statement),
            BoundKind.BlockStatement => ExpandBlockStatement((BoundBlockStatement)statement),
            BoundKind.LocalDeclarationStatement => ExpandLocalDeclarationStatement((BoundLocalDeclarationStatement)statement),
            BoundKind.IfStatement => ExpandIfStatement((BoundIfStatement)statement),
            BoundKind.WhileStatement => ExpandWhileStatement((BoundWhileStatement)statement),
            BoundKind.ForStatement => ExpandForStatement((BoundForStatement)statement),
            BoundKind.ExpressionStatement => ExpandExpressionStatement((BoundExpressionStatement)statement),
            BoundKind.LabelStatement => ExpandLabelStatement((BoundLabelStatement)statement),
            BoundKind.GotoStatement => ExpandGotoStatement((BoundGotoStatement)statement),
            BoundKind.ConditionalGotoStatement => ExpandConditionalGotoStatement((BoundConditionalGotoStatement)statement),
            BoundKind.DoWhileStatement => ExpandDoWhileStatement((BoundDoWhileStatement)statement),
            BoundKind.ReturnStatement => ExpandReturnStatement((BoundReturnStatement)statement),
            BoundKind.TryStatement => ExpandTryStatement((BoundTryStatement)statement),
            BoundKind.BreakStatement => ExpandBreakStatement((BoundBreakStatement)statement),
            BoundKind.ContinueStatement => ExpandContinueStatement((BoundContinueStatement)statement),
            BoundKind.ErrorStatement => ExpandErrorStatement((BoundErrorStatement)statement),
            BoundKind.LocalFunctionStatement => ExpandLocalFunctionStatement((BoundLocalFunctionStatement)statement),
            _ => throw new BelteInternalException($"ExpandStatement: unexpected expression type '{statement.kind}'"),
        };
    }

    private protected virtual List<BoundStatement> ExpandErrorStatement(BoundErrorStatement statement) {
        // Even though there is protential for expanding the childBoundNodes, it will be an error anyways so why bother
        return [statement];
    }

    private protected virtual List<BoundStatement> ExpandNopStatement(BoundNopStatement statement) {
        return [statement];
    }

    private protected virtual List<BoundStatement> ExpandLocalFunctionStatement(BoundLocalFunctionStatement statement) {
        // ExpandBlockStatement always returns a single block statement
        var newBody = (BoundBlockStatement)ExpandBlockStatement(statement.body)[0];
        return [new BoundLocalFunctionStatement(statement.syntax, statement.symbol, newBody)];
    }

    private protected virtual List<BoundStatement> ExpandBlockStatement(BoundBlockStatement statement) {
        var statements = new List<BoundStatement>();

        foreach (var childStatement in statement.statements)
            statements.AddRange(ExpandStatement(childStatement));

        return [Block(statement.syntax, statements.ToArray())];
    }

    private protected virtual List<BoundStatement> ExpandLocalDeclarationStatement(
        BoundLocalDeclarationStatement statement) {
        var statements = ExpandExpression(statement.declaration.initializer, out var replacement);
        var syntax = statement.syntax;

        if (statements.Count > 0) {
            statements.Add(new BoundLocalDeclarationStatement(
                syntax,
                new BoundDataContainerDeclaration(syntax, statement.declaration.dataContainer, replacement)
            ));

            return statements;
        }

        return [statement];
    }

    private protected virtual List<BoundStatement> ExpandIfStatement(BoundIfStatement statement) {
        var statements = ExpandExpression(statement.condition, out var conditionReplacement);
        var syntax = statement.syntax;

        statements.Add(
            new BoundIfStatement(
                syntax,
                conditionReplacement,
                Simplify(syntax, ExpandStatement(statement.consequence)),
                statement.alternative is not null ? Simplify(syntax, ExpandStatement(statement.alternative)) : null
            )
        );

        return statements;
    }

    private protected virtual List<BoundStatement> ExpandWhileStatement(BoundWhileStatement statement) {
        var statements = ExpandExpression(statement.condition, out var conditionReplacement);
        var syntax = statement.syntax;

        statements.Add(
            new BoundWhileStatement(
                syntax,
                conditionReplacement,
                Simplify(syntax, ExpandStatement(statement.body)),
                statement.breakLabel,
                statement.continueLabel
            )
        );

        return statements;
    }

    private protected virtual List<BoundStatement> ExpandForStatement(BoundForStatement statement) {
        // For loops have to be expanded after they have been lowered
        return [statement];
    }

    private protected virtual List<BoundStatement> ExpandExpressionStatement(BoundExpressionStatement statement) {
        var statements = ExpandExpression(statement.expression, out var replacement);

        if (statements.Count != 0) {
            statements.Add(new BoundExpressionStatement(statement.syntax, replacement));
            return statements;
        }

        return [statement];
    }

    private protected virtual List<BoundStatement> ExpandLabelStatement(BoundLabelStatement statement) {
        return [statement];
    }

    private protected virtual List<BoundStatement> ExpandGotoStatement(BoundGotoStatement statement) {
        return [statement];
    }

    private protected virtual List<BoundStatement> ExpandConditionalGotoStatement(BoundConditionalGotoStatement statement) {
        return [statement];
    }

    private protected virtual List<BoundStatement> ExpandDoWhileStatement(BoundDoWhileStatement statement) {
        var statements = ExpandExpression(statement.condition, out var conditionReplacement);
        var syntax = statement.syntax;

        statements.Add(
            new BoundDoWhileStatement(
                syntax,
                conditionReplacement,
                Simplify(syntax, ExpandStatement(statement.body)),
                statement.breakLabel,
                statement.continueLabel
            )
        );

        return statements;
    }

    private protected virtual List<BoundStatement> ExpandReturnStatement(BoundReturnStatement statement) {
        if (statement.expression is null)
            return [statement];

        var statements = ExpandExpression(statement.expression, out var replacement);

        if (statements.Count != 0) {
            statements.Add(new BoundReturnStatement(statement.syntax, statement.refKind, replacement));
            return statements;
        }

        return [statement];
    }

    private protected virtual List<BoundStatement> ExpandTryStatement(BoundTryStatement statement) {
        var syntax = statement.syntax;

        return [
            new BoundTryStatement(
                syntax,
                Simplify(syntax, ExpandStatement(statement.body)) as BoundBlockStatement,
                statement.catchBody is not null ?
                    Simplify(syntax, ExpandStatement(statement.catchBody)) as BoundBlockStatement
                    : null,
                statement.finallyBody is not null ?
                    Simplify(syntax, ExpandStatement(statement.finallyBody)) as BoundBlockStatement
                    : null
            )
        ];
    }

    private protected virtual List<BoundStatement> ExpandBreakStatement(BoundBreakStatement statement) {
        return [statement];
    }

    private protected virtual List<BoundStatement> ExpandContinueStatement(BoundContinueStatement statement) {
        return [statement];
    }

    private protected virtual List<BoundStatement> ExpandExpression(
        BoundExpression expression,
        out BoundExpression replacement) {
        return expression.kind switch {
            BoundKind.LiteralExpression => ExpandLiteralExpression((BoundLiteralExpression)expression, out replacement),
            BoundKind.InitializerList => ExpandInitializerList((BoundInitializerList)expression, out replacement),
            BoundKind.InitializerDictionary => ExpandInitializerDictionary((BoundInitializerDictionary)expression, out replacement),
            BoundKind.DataContainerExpression => ExpandDataContainerExpression((BoundDataContainerExpression)expression, out replacement),
            BoundKind.AssignmentOperator => ExpandAssignmentOperator((BoundAssignmentOperator)expression, out replacement),
            BoundKind.UnaryOperator => ExpandUnaryOperator((BoundUnaryOperator)expression, out replacement),
            BoundKind.IncrementOperator => ExpandIncrementOperator((BoundIncrementOperator)expression, out replacement),
            BoundKind.BinaryOperator => ExpandBinaryOperator((BoundBinaryOperator)expression, out replacement),
            BoundKind.AsOperator => ExpandAsOperator((BoundAsOperator)expression, out replacement),
            BoundKind.IsOperator => ExpandIsOperator((BoundIsOperator)expression, out replacement),
            BoundKind.NullCoalescingOperator => ExpandNullCoalescingOperator((BoundNullCoalescingOperator)expression, out replacement),
            BoundKind.NullCoalescingAssignmentOperator => ExpandNullCoalescingAssignmentOperator((BoundNullCoalescingAssignmentOperator)expression, out replacement),
            BoundKind.NullAssertOperator => ExpandNullAssertOperator((BoundNullAssertOperator)expression, out replacement),
            BoundKind.ErrorExpression => ExpandErrorExpression((BoundErrorExpression)expression, out replacement),
            BoundKind.CallExpression => ExpandCallExpression((BoundCallExpression)expression, out replacement),
            BoundKind.CastExpression => ExpandCastExpression((BoundCastExpression)expression, out replacement),
            BoundKind.ArrayAccessExpression => ExpandArrayAccessExpression((BoundArrayAccessExpression)expression, out replacement),
            BoundKind.CompoundAssignmentOperator => ExpandCompoundAssignmentOperator((BoundCompoundAssignmentOperator)expression, out replacement),
            BoundKind.ReferenceExpression => ExpandReferenceExpression((BoundReferenceExpression)expression, out replacement),
            BoundKind.TypeOfExpression => ExpandTypeOfExpression((BoundTypeOfExpression)expression, out replacement),
            BoundKind.ConditionalOperator => ExpandConditionalOperator((BoundConditionalOperator)expression, out replacement),
            BoundKind.ObjectCreationExpression => ExpandObjectCreationExpression((BoundObjectCreationExpression)expression, out replacement),
            BoundKind.ArrayCreationExpression => ExpandArrayCreationExpression((BoundArrayCreationExpression)expression, out replacement),
            BoundKind.FieldAccessExpression => ExpandFieldAccessExpression((BoundFieldAccessExpression)expression, out replacement),
            BoundKind.ConditionalAccessExpression => ExpandConditionalAccessExpression((BoundConditionalAccessExpression)expression, out replacement),
            BoundKind.ThisExpression => ExpandThisExpression((BoundThisExpression)expression, out replacement),
            BoundKind.BaseExpression => ExpandBaseExpression((BoundBaseExpression)expression, out replacement),
            BoundKind.ThrowExpression => ExpandThrowExpression((BoundThrowExpression)expression, out replacement),
            BoundKind.TypeExpression => ExpandTypeExpression((BoundTypeExpression)expression, out replacement),
            BoundKind.ParameterExpression => ExpandParameterExpression((BoundParameterExpression)expression, out replacement),
            _ => throw new BelteInternalException($"ExpandExpression: unexpected expression type '{expression.kind}'"),
        };
    }

    private protected virtual List<BoundStatement> ExpandParameterExpression(
        BoundParameterExpression expression,
        out BoundExpression replacement) {
        replacement = expression;
        return [];
    }

    private protected virtual List<BoundStatement> ExpandTypeExpression(
        BoundTypeExpression expression,
        out BoundExpression replacement) {
        replacement = expression;
        return [];
    }

    private protected virtual List<BoundStatement> ExpandThisExpression(
        BoundThisExpression expression,
        out BoundExpression replacement) {
        replacement = expression;
        return [];
    }

    private protected virtual List<BoundStatement> ExpandBaseExpression(
        BoundBaseExpression expression,
        out BoundExpression replacement) {
        replacement = expression;
        return [];
    }

    private protected virtual List<BoundStatement> ExpandThrowExpression(
        BoundThrowExpression expression,
        out BoundExpression replacement) {
        replacement = expression;
        return [];
    }

    private protected virtual List<BoundStatement> ExpandBinaryOperator(
        BoundBinaryOperator expression,
        out BoundExpression replacement) {
        var statements = ExpandExpression(expression.left, out var newLeft);
        statements.AddRange(ExpandExpression(expression.right, out var newRight));

        if (statements.Count != 0) {
            replacement = expression.Update(
                newLeft,
                newRight,
                expression.operatorKind,
                expression.constantValue,
                expression.type
            );

            return statements;
        }

        replacement = expression;
        return [];
    }

    private protected virtual List<BoundStatement> ExpandAsOperator(
        BoundAsOperator expression,
        out BoundExpression replacement) {
        var statements = ExpandExpression(expression.left, out var newLeft);
        statements.AddRange(ExpandExpression(expression.right, out var newRight));

        if (statements.Count != 0) {
            replacement = expression.Update(
                newLeft,
                newRight,
                expression.operandPlaceholder,
                expression.operandConversion,
                expression.type
            );

            return statements;
        }

        replacement = expression;
        return [];
    }

    private protected virtual List<BoundStatement> ExpandIsOperator(
        BoundIsOperator expression,
        out BoundExpression replacement) {
        var statements = ExpandExpression(expression.left, out var newLeft);
        statements.AddRange(ExpandExpression(expression.right, out var newRight));

        if (statements.Count != 0) {
            replacement = expression.Update(
                newLeft,
                newRight,
                expression.isNot,
                expression.constantValue,
                expression.type
            );

            return statements;
        }

        replacement = expression;
        return [];
    }

    private protected virtual List<BoundStatement> ExpandNullCoalescingOperator(
        BoundNullCoalescingOperator expression,
        out BoundExpression replacement) {
        var statements = ExpandExpression(expression.left, out var newLeft);
        statements.AddRange(ExpandExpression(expression.right, out var newRight));

        if (statements.Count != 0) {
            replacement = expression.Update(newLeft, newRight, expression.constantValue, expression.type);
            return statements;
        }

        replacement = expression;
        return [];
    }

    private protected virtual List<BoundStatement> ExpandNullCoalescingAssignmentOperator(
        BoundNullCoalescingAssignmentOperator expression,
        out BoundExpression replacement) {
        var statements = ExpandExpression(expression.left, out var newLeft);
        statements.AddRange(ExpandExpression(expression.right, out var newRight));

        if (statements.Count != 0) {
            replacement = expression.Update(newLeft, newRight, expression.type);
            return statements;
        }

        replacement = expression;
        return [];
    }

    private protected virtual List<BoundStatement> ExpandInitializerList(
        BoundInitializerList expression,
        out BoundExpression replacement) {
        var statements = new List<BoundStatement>();
        var replacementItems = ArrayBuilder<BoundExpression>.GetInstance();

        foreach (var item in expression.items) {
            statements.AddRange(ExpandExpression(item, out var itemReplacement));
            replacementItems.Add(itemReplacement);
        }

        if (statements.Count != 0) {
            replacement = expression.Update(replacementItems.ToImmutableAndFree(), expression.type);
            return statements;
        }

        replacement = expression;
        return [];
    }

    private protected virtual List<BoundStatement> ExpandInitializerDictionary(
        BoundInitializerDictionary expression,
        out BoundExpression replacement) {
        replacement = expression;
        return [];
    }

    private protected virtual List<BoundStatement> ExpandLiteralExpression(
        BoundLiteralExpression expression,
        out BoundExpression replacement) {
        replacement = expression;
        return [];
    }

    private protected virtual List<BoundStatement> ExpandDataContainerExpression(
        BoundDataContainerExpression expression,
        out BoundExpression replacement) {
        replacement = expression;
        return [];
    }

    private protected virtual List<BoundStatement> ExpandAssignmentOperator(
        BoundAssignmentOperator expression,
        out BoundExpression replacement) {
        var statements = ExpandExpression(expression.left, out var newLeft);
        statements.AddRange(ExpandExpression(expression.right, out var newRight));

        if (statements.Count != 0) {
            replacement = expression.Update(newLeft, newRight, expression.isRef, expression.type);
            return statements;
        }

        replacement = expression;
        return [];
    }

    private protected virtual List<BoundStatement> ExpandUnaryOperator(
        BoundUnaryOperator expression,
        out BoundExpression replacement) {
        var statements = ExpandExpression(expression.operand, out var newOperand);

        if (statements.Count != 0) {
            replacement = expression.Update(
                newOperand,
                expression.operatorKind,
                expression.constantValue,
                expression.type
            );

            return statements;
        }

        replacement = expression;
        return [];
    }

    private protected virtual List<BoundStatement> ExpandIncrementOperator(
        BoundIncrementOperator expression,
        out BoundExpression replacement) {
        var statements = ExpandExpression(expression.operand, out var newOperand);

        if (statements.Count != 0) {
            replacement = expression.Update(
                expression.operatorKind,
                newOperand,
                expression.operandPlaceholder,
                expression.operandConversion,
                expression.resultPlaceholder,
                expression.resultConversion,
                expression.resultKind,
                expression.originalUserDefinedOperators,
                expression.type
            );

            return statements;
        }

        replacement = expression;
        return [];
    }

    private protected virtual List<BoundStatement> ExpandNullAssertOperator(
        BoundNullAssertOperator expression,
        out BoundExpression replacement) {
        var statements = ExpandExpression(expression.operand, out var newOperand);

        if (statements.Count != 0) {
            replacement = expression.Update(newOperand, expression.constantValue, expression.type);
            return statements;
        }

        replacement = expression;
        return [];
    }

    private protected virtual List<BoundStatement> ExpandErrorExpression(
        BoundErrorExpression expression,
        out BoundExpression replacement) {
        replacement = expression;
        return [];
    }

    private protected virtual List<BoundStatement> ExpandCallExpression(
        BoundCallExpression expression,
        out BoundExpression replacement) {
        List<BoundStatement> statements;
        BoundExpression newReceiver = null;

        if (expression.receiver is not null)
            statements = ExpandExpression(expression.receiver, out newReceiver);
        else
            statements = [];

        statements.AddRange(ExpandArguments(expression.arguments, out var newArguments));

        replacement = expression.Update(
            newReceiver,
            expression.method,
            newArguments,
            expression.argumentRefKinds,
            expression.defaultArguments,
            expression.resultKind,
            expression.type
        );

        return statements;
    }

    private List<BoundStatement> ExpandArguments(
        ImmutableArray<BoundExpression> arguments,
        out ImmutableArray<BoundExpression> replacement) {
        var statements = new List<BoundStatement>();
        var replacementArguments = ArrayBuilder<BoundExpression>.GetInstance();

        foreach (var argument in arguments) {
            statements.AddRange(ExpandExpression(argument, out var newArgument));
            replacementArguments.Add(newArgument);
        }

        replacement = replacementArguments.ToImmutableAndFree();
        return statements;
    }

    private protected virtual List<BoundStatement> ExpandCastExpression(
        BoundCastExpression expression,
        out BoundExpression replacement) {
        var statements = ExpandExpression(expression.operand, out var newOperand);

        if (statements.Count != 0) {
            replacement = expression.Update(
                newOperand,
                expression.conversion,
                expression.constantValue,
                expression.type
            );

            return statements;
        }

        replacement = expression;
        return [];
    }

    private protected virtual List<BoundStatement> ExpandArrayAccessExpression(
        BoundArrayAccessExpression expression,
        out BoundExpression replacement) {
        var statements = ExpandExpression(expression.receiver, out var newOperand);
        statements.AddRange(ExpandExpression(expression.index, out var newIndex));

        if (statements.Count != 0) {
            replacement = expression.Update(newOperand, newIndex, expression.constantValue, expression.type);
            return statements;
        }

        replacement = expression;
        return [];
    }

    private protected virtual List<BoundStatement> ExpandCompoundAssignmentOperator(
        BoundCompoundAssignmentOperator expression,
        out BoundExpression replacement) {
        var statements = ExpandExpression(expression.left, out var newLeft);
        statements.AddRange(ExpandExpression(expression.right, out var newRight));

        if (statements.Count != 0) {
            replacement = expression.Update(
                newLeft,
                newRight,
                expression.op,
                expression.leftPlaceholder,
                expression.leftConversion,
                expression.finalPlaceholder,
                expression.finalConversion,
                expression.resultKind,
                expression.originalUserDefinedOperators,
                expression.type
            );

            return statements;
        }

        replacement = expression;
        return [];
    }

    private protected virtual List<BoundStatement> ExpandReferenceExpression(
        BoundReferenceExpression expression,
        out BoundExpression replacement) {
        replacement = expression;
        return [];
    }

    private protected virtual List<BoundStatement> ExpandTypeOfExpression(
        BoundTypeOfExpression expression,
        out BoundExpression replacement) {
        replacement = expression;
        return [];
    }

    private protected virtual List<BoundStatement> ExpandConditionalOperator(
        BoundConditionalOperator expression,
        out BoundExpression replacement) {
        var statements = ExpandExpression(expression.condition, out var newCondition);
        statements.AddRange(ExpandExpression(expression.trueExpression, out var newTrueExpression));
        statements.AddRange(ExpandExpression(expression.falseExpression, out var newFalseExpression));

        if (statements.Count != 0) {
            replacement = expression.Update(
                newCondition,
                expression.isRef,
                newTrueExpression,
                newFalseExpression,
                expression.constantValue,
                expression.type
            );

            return statements;
        }

        replacement = expression;
        return [];
    }

    private protected virtual List<BoundStatement> ExpandObjectCreationExpression(
        BoundObjectCreationExpression expression,
        out BoundExpression replacement) {
        var statements = ExpandArguments(expression.arguments, out var newArguments);

        replacement = expression.Update(
            expression.constructor,
            newArguments,
            expression.argumentRefKinds,
            expression.argsToParams,
            expression.defaultArguments,
            expression.wasTargetTyped,
            expression.type
        );

        return statements;
    }

    private protected virtual List<BoundStatement> ExpandArrayCreationExpression(
        BoundArrayCreationExpression expression,
        out BoundExpression replacement) {
        var statements = ExpandArguments(expression.sizes, out var newSizes);
        replacement = expression.Update(newSizes, expression.type);
        return statements;
    }

    private protected virtual List<BoundStatement> ExpandFieldAccessExpression(
        BoundFieldAccessExpression expression,
        out BoundExpression replacement) {
        var statements = ExpandExpression(expression.receiver, out var newReceiver);

        if (statements.Count != 0) {
            replacement = expression.Update(newReceiver, expression.field, expression.constantValue, expression.type);
            return statements;
        }

        replacement = expression;
        return [];
    }

    private protected virtual List<BoundStatement> ExpandConditionalAccessExpression(
        BoundConditionalAccessExpression expression,
        out BoundExpression replacement) {
        var statements = ExpandExpression(expression.receiver, out var newReceiver);
        statements.AddRange(ExpandExpression(expression.accessExpression, out var newAccess));

        if (statements.Count != 0) {
            replacement = expression.Update(newReceiver, newAccess, expression.type);
            return statements;
        }

        replacement = expression;
        return [];
    }
}
