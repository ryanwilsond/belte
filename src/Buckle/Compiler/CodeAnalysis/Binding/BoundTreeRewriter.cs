using System.Collections.Immutable;
using Buckle.Diagnostics;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// Rewrites BoundStatements and all child BoundStatements.
/// </summary>
internal abstract class BoundTreeRewriter {
    /// <summary>
    /// Rewrites a single <see cref="Syntax.StatementSyntax" /> (including all children, recursive).
    /// </summary>
    /// <param name="statement"><see cref="Syntax.StatementSyntax" /> to rewrite.</param>
    /// <returns>
    /// New <see cref="Syntax.StatementSyntax" /> or input <see cref="Syntax.StatementSyntax" /> if nothing changed.
    /// </returns>
    internal virtual BoundStatement RewriteStatement(BoundStatement statement) {
        return statement.kind switch {
            BoundNodeKind.NopStatement => RewriteNopStatement((BoundNopStatement)statement),
            BoundNodeKind.BlockStatement => RewriteBlockStatement((BoundBlockStatement)statement),
            BoundNodeKind.LocalDeclarationStatement
                => RewriteLocalDeclarationStatement((BoundLocalDeclarationStatement)statement),
            BoundNodeKind.IfStatement => RewriteIfStatement((BoundIfStatement)statement),
            BoundNodeKind.WhileStatement => RewriteWhileStatement((BoundWhileStatement)statement),
            BoundNodeKind.ForStatement => RewriteForStatement((BoundForStatement)statement),
            BoundNodeKind.ExpressionStatement => RewriteExpressionStatement((BoundExpressionStatement)statement),
            BoundNodeKind.LabelStatement => RewriteLabelStatement((BoundLabelStatement)statement),
            BoundNodeKind.GotoStatement => RewriteGotoStatement((BoundGotoStatement)statement),
            BoundNodeKind.ConditionalGotoStatement
                => RewriteConditionalGotoStatement((BoundConditionalGotoStatement)statement),
            BoundNodeKind.DoWhileStatement => RewriteDoWhileStatement((BoundDoWhileStatement)statement),
            BoundNodeKind.ReturnStatement => RewriteReturnStatement((BoundReturnStatement)statement),
            BoundNodeKind.TryStatement => RewriteTryStatement((BoundTryStatement)statement),
            BoundNodeKind.BreakStatement => RewriteBreakStatement((BoundBreakStatement)statement),
            BoundNodeKind.ContinueStatement => RewriteContinueStatement((BoundContinueStatement)statement),
            _ => throw new BelteInternalException($"RewriteStatement: unexpected expression type '{statement.kind}'"),
        };
    }

    private protected virtual BoundStatement RewriteContinueStatement(BoundContinueStatement statement) {
        return statement;
    }

    private protected virtual BoundStatement RewriteBreakStatement(BoundBreakStatement statement) {
        return statement;
    }

    private protected virtual BoundStatement RewriteTryStatement(BoundTryStatement statement) {
        var body = (BoundBlockStatement)RewriteBlockStatement(statement.body);
        var catchBody = statement.catchBody is null
            ? null
            : (BoundBlockStatement)RewriteBlockStatement(statement.catchBody);

        var finallyBody = statement.finallyBody is null
            ? null
            : (BoundBlockStatement)RewriteBlockStatement(statement.finallyBody);

        if (body == statement.body && catchBody == statement.catchBody && finallyBody == statement.finallyBody)
            return statement;

        return new BoundTryStatement(body, catchBody, finallyBody);
    }

    private protected virtual BoundStatement RewriteNopStatement(BoundNopStatement statement) {
        return statement;
    }

    private protected virtual BoundStatement RewriteReturnStatement(BoundReturnStatement statement) {
        var expression = statement.expression is null ? null : RewriteExpression(statement.expression);

        if (expression == statement.expression)
            return statement;

        return new BoundReturnStatement(expression);
    }

    private protected virtual BoundStatement RewriteDoWhileStatement(BoundDoWhileStatement statement) {
        var body = RewriteStatement(statement.body);
        var condition = RewriteExpression(statement.condition);

        if (body == statement.body && condition == statement.condition)
            return statement;

        return new BoundDoWhileStatement(body, condition, statement.breakLabel, statement.continueLabel);
    }

    private protected virtual BoundStatement RewriteLabelStatement(BoundLabelStatement statement) {
        return statement;
    }

    private protected virtual BoundStatement RewriteGotoStatement(BoundGotoStatement statement) {
        return statement;
    }

    private protected virtual BoundStatement RewriteConditionalGotoStatement(BoundConditionalGotoStatement statement) {
        var condition = RewriteExpression(statement.condition);

        if (condition == statement.condition)
            return statement;

        return new BoundConditionalGotoStatement(statement.label, condition, statement.jumpIfTrue);
    }

    private protected virtual BoundStatement RewriteExpressionStatement(BoundExpressionStatement statement) {
        var expression = RewriteExpression(statement.expression);

        if (expression == statement.expression)
            return statement;

        return new BoundExpressionStatement(expression);
    }

    private protected virtual BoundStatement RewriteForStatement(BoundForStatement statement) {
        var condition = RewriteExpression(statement.condition);
        var initializer = RewriteStatement(statement.initializer);
        var step = RewriteExpression(statement.step);
        var body = RewriteStatement(statement.body);

        if (initializer == statement.initializer && condition == statement.condition &&
            step == statement.step && body == statement.body) {
            return statement;
        }

        return new BoundForStatement(initializer, condition, step, body, statement.breakLabel, statement.continueLabel);
    }

    private protected virtual BoundStatement RewriteWhileStatement(BoundWhileStatement statement) {
        var condition = RewriteExpression(statement.condition);
        var body = RewriteStatement(statement.body);

        if (condition == statement.condition && body == statement.body)
            return statement;

        return new BoundWhileStatement(condition, body, statement.breakLabel, statement.continueLabel);
    }

    private protected virtual BoundStatement RewriteIfStatement(BoundIfStatement statement) {
        var condition = RewriteExpression(statement.condition);
        var then = RewriteStatement(statement.then);
        var elseStatement = statement.elseStatement is null ? null : RewriteStatement(statement.elseStatement);

        if (condition == statement.condition && then == statement.then && elseStatement == statement.elseStatement)
            return statement;

        return new BoundIfStatement(condition, then, elseStatement);
    }

    private protected virtual BoundStatement RewriteLocalDeclarationStatement(BoundLocalDeclarationStatement statement) {
        var initializer = RewriteExpression(statement.declaration.initializer);

        if (initializer == statement.declaration.initializer)
            return statement;

        return new BoundLocalDeclarationStatement(
            new BoundVariableDeclaration(statement.declaration.variable, initializer)
        );
    }

    private protected virtual BoundStatement RewriteBlockStatement(BoundBlockStatement statement) {
        ArrayBuilder<BoundStatement> builder = null;

        for (var i = 0; i < statement.statements.Length; i++) {
            var oldStatement = statement.statements[i];
            var newStatement = RewriteStatement(oldStatement);

            if (newStatement != oldStatement && builder is null) {
                builder = ArrayBuilder<BoundStatement>.GetInstance(statement.statements.Length);

                for (var j = 0; j < i; j++)
                    builder.Add(statement.statements[j]);
            }

            builder?.Add(newStatement);
        }

        if (builder is null)
            return statement;

        return new BoundBlockStatement(builder.ToImmutableAndFree());
    }

    internal virtual BoundExpression RewriteExpression(BoundExpression expression) {
        if (expression.constantValue is not null)
            return RewriteConstantExpression(expression);

        switch (expression.kind) {
            case BoundNodeKind.LiteralExpression:
                return RewriteLiteralExpression((BoundLiteralExpression)expression);
            case BoundNodeKind.InitializerListExpression:
                return RewriteInitializerListExpression((BoundInitializerListExpression)expression);
            case BoundNodeKind.InitializerDictionaryExpression:
                return RewriteInitializerDictionaryExpression((BoundInitializerDictionaryExpression)expression);
            case BoundNodeKind.VariableExpression:
                return RewriteVariableExpression((BoundVariableExpression)expression);
            case BoundNodeKind.AssignmentExpression:
                return RewriteAssignmentExpression((BoundAssignmentExpression)expression);
            case BoundNodeKind.UnaryExpression:
                return RewriteUnaryExpression((BoundUnaryExpression)expression);
            case BoundNodeKind.BinaryExpression:
                return RewriteBinaryExpression((BoundBinaryExpression)expression);
            case BoundNodeKind.EmptyExpression:
                return RewriteEmptyExpression((BoundEmptyExpression)expression);
            case BoundNodeKind.ErrorExpression:
                return RewriteErrorExpression((BoundErrorExpression)expression);
            case BoundNodeKind.CallExpression:
                return RewriteCallExpression((BoundCallExpression)expression);
            case BoundNodeKind.CastExpression:
                return RewriteCastExpression((BoundCastExpression)expression);
            case BoundNodeKind.IndexExpression:
                return RewriteIndexExpression((BoundIndexExpression)expression);
            case BoundNodeKind.CompoundAssignmentExpression:
                return RewriteCompoundAssignmentExpression((BoundCompoundAssignmentExpression)expression);
            case BoundNodeKind.ReferenceExpression:
                return RewriteReferenceExpression((BoundReferenceExpression)expression);
            case BoundNodeKind.TypeOfExpression:
                return RewriteTypeOfExpression((BoundTypeOfExpression)expression);
            case BoundNodeKind.TernaryExpression:
                return RewriteTernaryExpression((BoundTernaryExpression)expression);
            case BoundNodeKind.ObjectCreationExpression:
                return RewriteObjectCreationExpression((BoundObjectCreationExpression)expression);
            case BoundNodeKind.MemberAccessExpression:
                return RewriteMemberAccessExpression((BoundMemberAccessExpression)expression);
            case BoundNodeKind.PrefixExpression:
                return RewritePrefixExpression((BoundPrefixExpression)expression);
            case BoundNodeKind.PostfixExpression:
                return RewritePostfixExpression((BoundPostfixExpression)expression);
            case BoundNodeKind.ThisExpression:
                return RewriteThisExpression((BoundThisExpression)expression);
            case BoundNodeKind.BaseExpression:
                return RewriteBaseExpression((BoundBaseExpression)expression);
            case BoundNodeKind.ThrowExpression:
                return RewriteThrowExpression((BoundThrowExpression)expression);
            case BoundNodeKind.Type:
                return RewriteType((BoundType)expression);
            default:
                throw new BelteInternalException($"RewriteExpression: unexpected expression type '{expression.kind}'");
        }
    }

    private protected virtual BoundExpression RewriteType(BoundType expression) {
        return expression;
    }

    private protected virtual BoundExpression RewriteThisExpression(BoundThisExpression expression) {
        return expression;
    }

    private protected virtual BoundExpression RewriteBaseExpression(BoundBaseExpression expression) {
        return expression;
    }

    private protected virtual BoundExpression RewriteThrowExpression(BoundThrowExpression expression) {
        var exception = RewriteExpression(expression.exception);

        if (exception == expression.exception)
            return expression;

        return new BoundThrowExpression(exception);
    }

    private protected virtual BoundExpression RewriteConstantExpression(BoundExpression expression) {
        if (expression.constantValue.value is ImmutableArray<ConstantValue>)
            return new BoundInitializerListExpression(expression.constantValue, expression.type);
        else if (expression is not BoundTypeWrapper)
            return new BoundLiteralExpression(expression.constantValue.value, expression.type);
        else
            return expression;
    }

    private protected virtual BoundExpression RewritePrefixExpression(BoundPrefixExpression expression) {
        var operand = RewriteExpression(expression.operand);

        if (operand == expression.operand)
            return expression;

        return new BoundPrefixExpression(expression.op, operand);
    }

    private protected virtual BoundExpression RewritePostfixExpression(BoundPostfixExpression expression) {
        var operand = RewriteExpression(expression.operand);

        if (operand == expression.operand)
            return expression;

        return new BoundPostfixExpression(operand, expression.op, expression.isOwnStatement);
    }

    private protected virtual BoundExpression RewriteMemberAccessExpression(BoundMemberAccessExpression expression) {
        var left = RewriteExpression(expression.left);
        var right = RewriteExpression(expression.right);

        if (left == expression.left && right == expression.right)
            return expression;

        return new BoundMemberAccessExpression(
            left,
            right,
            expression.isNullConditional,
            expression.isStaticAccess
        );
    }

    private protected virtual BoundExpression RewriteObjectCreationExpression(BoundObjectCreationExpression expression) {
        if (expression.viaConstructor) {
            var arguments = RewriteArguments(expression.arguments);

            if (!arguments.HasValue)
                return expression;

            return new BoundObjectCreationExpression(expression.type, expression.constructor, arguments.Value);
        } else {
            return expression;
        }
    }

    private protected virtual BoundExpression RewriteTernaryExpression(BoundTernaryExpression expression) {
        var left = RewriteExpression(expression.left);
        var center = RewriteExpression(expression.center);
        var right = RewriteExpression(expression.right);

        if (left == expression.left && center == expression.center && right == expression.right)
            return expression;

        return new BoundTernaryExpression(left, expression.op, center, right);
    }

    private protected virtual BoundExpression RewriteTypeOfExpression(BoundTypeOfExpression expression) {
        return expression;
    }

    private protected virtual BoundExpression RewriteReferenceExpression(BoundReferenceExpression expression) {
        return expression;
    }

    private protected virtual BoundExpression RewriteIndexExpression(BoundIndexExpression expression) {
        var index = RewriteExpression(expression.index);

        if (index == expression.index)
            return expression;

        return new BoundIndexExpression(expression.expression, index, expression.isNullConditional);
    }

    private protected virtual BoundExpression RewriteInitializerListExpression(BoundInitializerListExpression expression) {
        ArrayBuilder<BoundExpression> builder = null;

        for (var i = 0; i < expression.items.Length; i++) {
            var oldItem = expression.items[i];
            var newItem = RewriteExpression(oldItem);

            if (newItem != oldItem) {
                if (builder is null) {
                    builder = ArrayBuilder<BoundExpression>.GetInstance(expression.items.Length);

                    for (var j = 0; j < i; j++)
                        builder.Add(expression.items[j]);
                }
            }

            builder?.Add(newItem);
        }

        if (builder is null)
            return expression;

        return new BoundInitializerListExpression(builder.ToImmutableAndFree(), expression.type);
    }

    private protected virtual BoundExpression RewriteInitializerDictionaryExpression(
        BoundInitializerDictionaryExpression expression) {
        return expression;
    }

    private protected virtual BoundExpression RewriteCompoundAssignmentExpression(
        BoundCompoundAssignmentExpression expression) {
        var left = RewriteExpression(expression.left);
        var right = RewriteExpression(expression.right);

        if (left == expression.left && right == expression.right)
            return expression;

        return new BoundCompoundAssignmentExpression(left, expression.op, right);
    }

    private protected virtual BoundExpression RewriteCastExpression(BoundCastExpression expression) {
        var rewrote = RewriteExpression(expression.operand);

        if (rewrote == expression.operand)
            return expression;

        return new BoundCastExpression(expression.type, rewrote);
    }

    private protected virtual BoundExpression RewriteCallExpression(BoundCallExpression expression) {
        var rewrote = RewriteExpression(expression.expression);
        var arguments = RewriteArguments(expression.arguments);

        if (!arguments.HasValue)
            return expression;

        return new BoundCallExpression(rewrote, expression.method, arguments.Value, expression.templateArguments);
    }

    private protected virtual BoundExpression RewriteErrorExpression(BoundErrorExpression expression) {
        return expression;
    }

    private protected virtual BoundExpression RewriteEmptyExpression(BoundEmptyExpression expression) {
        return expression;
    }

    private protected virtual BoundExpression RewriteBinaryExpression(BoundBinaryExpression expression) {
        var left = RewriteExpression(expression.left);
        var right = RewriteExpression(expression.right);

        if (left == expression.left && right == expression.right)
            return expression;

        return new BoundBinaryExpression(left, expression.op, right);
    }

    private protected virtual BoundExpression RewriteLiteralExpression(BoundLiteralExpression expression) {
        return expression;
    }

    private protected virtual BoundExpression RewriteVariableExpression(BoundVariableExpression expression) {
        return expression;
    }

    private protected virtual BoundExpression RewriteAssignmentExpression(BoundAssignmentExpression expression) {
        var left = RewriteExpression(expression.left);
        var right = RewriteExpression(expression.right);

        if (left == expression.left && right == expression.right)
            return expression;

        return new BoundAssignmentExpression(left, right);
    }

    private protected virtual BoundExpression RewriteUnaryExpression(BoundUnaryExpression expression) {
        var operand = RewriteExpression(expression.operand);

        if (operand == expression.operand)
            return expression;

        return new BoundUnaryExpression(expression.op, operand);
    }

    private ImmutableArray<BoundExpression>? RewriteArguments(ImmutableArray<BoundExpression> arguments) {
        ArrayBuilder<BoundExpression> builder = null;

        for (var i = 0; i < arguments.Length; i++) {
            var oldArgument = arguments[i];
            var newArgument = RewriteExpression(oldArgument);

            if (newArgument != oldArgument) {
                if (builder is null) {
                    builder = ArrayBuilder<BoundExpression>.GetInstance(arguments.Length);

                    for (var j = 0; j < i; j++)
                        builder.Add(arguments[j]);
                }
            }

            builder?.Add(newArgument);
        }

        return builder?.ToImmutableAndFree();
    }
}
