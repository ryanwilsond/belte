using System.Collections.Immutable;
using Buckle.Diagnostics;

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
        switch (statement.kind) {
            case BoundNodeKind.NopStatement:
                return RewriteNopStatement((BoundNopStatement)statement);
            case BoundNodeKind.BlockStatement:
                return RewriteBlockStatement((BoundBlockStatement)statement);
            case BoundNodeKind.VariableDeclarationStatement:
                return RewriteVariableDeclarationStatement((BoundVariableDeclarationStatement)statement);
            case BoundNodeKind.IfStatement:
                return RewriteIfStatement((BoundIfStatement)statement);
            case BoundNodeKind.WhileStatement:
                return RewriteWhileStatement((BoundWhileStatement)statement);
            case BoundNodeKind.ForStatement:
                return RewriteForStatement((BoundForStatement)statement);
            case BoundNodeKind.ExpressionStatement:
                return RewriteExpressionStatement((BoundExpressionStatement)statement);
            case BoundNodeKind.LabelStatement:
                return RewriteLabelStatement((BoundLabelStatement)statement);
            case BoundNodeKind.GotoStatement:
                return RewriteGotoStatement((BoundGotoStatement)statement);
            case BoundNodeKind.ConditionalGotoStatement:
                return RewriteConditionalGotoStatement((BoundConditionalGotoStatement)statement);
            case BoundNodeKind.DoWhileStatement:
                return RewriteDoWhileStatement((BoundDoWhileStatement)statement);
            case BoundNodeKind.ReturnStatement:
                return RewriteReturnStatement((BoundReturnStatement)statement);
            case BoundNodeKind.TryStatement:
                return RewriteTryStatement((BoundTryStatement)statement);
            case BoundNodeKind.BreakStatement:
                return RewriteBreakStatement((BoundBreakStatement)statement);
            case BoundNodeKind.ContinueStatement:
                return RewriteContinueStatement((BoundContinueStatement)statement);
            default:
                throw new BelteInternalException($"RewriteStatement: unexpected expression type '{statement.kind}'");
        }
    }

    protected virtual BoundStatement RewriteContinueStatement(BoundContinueStatement statement) {
        return statement;
    }

    protected virtual BoundStatement RewriteBreakStatement(BoundBreakStatement statement) {
        return statement;
    }

    protected virtual BoundStatement RewriteTryStatement(BoundTryStatement statement) {
        var body = (BoundBlockStatement)RewriteBlockStatement(statement.body);
        var catchBody = statement.catchBody == null
            ? null
            : (BoundBlockStatement)RewriteBlockStatement(statement.catchBody);

        var finallyBody = statement.finallyBody == null
            ? null
            : (BoundBlockStatement)RewriteBlockStatement(statement.finallyBody);

        if (body == statement.body && catchBody == statement.catchBody && finallyBody == statement.finallyBody)
            return statement;

        return new BoundTryStatement(body, catchBody, finallyBody);
    }

    protected virtual BoundStatement RewriteNopStatement(BoundNopStatement statement) {
        return statement;
    }

    protected virtual BoundStatement RewriteReturnStatement(BoundReturnStatement statement) {
        var expression = statement.expression == null ? null : RewriteExpression(statement.expression);

        if (expression == statement.expression)
            return statement;

        return new BoundReturnStatement(expression);
    }

    protected virtual BoundStatement RewriteDoWhileStatement(BoundDoWhileStatement statement) {
        var body = RewriteStatement(statement.body);
        var condition = RewriteExpression(statement.condition);

        if (body == statement.body && condition == statement.condition)
            return statement;

        return new BoundDoWhileStatement(body, condition, statement.breakLabel, statement.continueLabel);
    }

    protected virtual BoundStatement RewriteLabelStatement(BoundLabelStatement statement) {
        return statement;
    }

    protected virtual BoundStatement RewriteGotoStatement(BoundGotoStatement statement) {
        return statement;
    }

    protected virtual BoundStatement RewriteConditionalGotoStatement(BoundConditionalGotoStatement statement) {
        var condition = RewriteExpression(statement.condition);

        if (condition == statement.condition)
            return statement;

        return new BoundConditionalGotoStatement(statement.label, condition, statement.jumpIfTrue);
    }

    protected virtual BoundStatement RewriteExpressionStatement(BoundExpressionStatement statement) {
        var expression = RewriteExpression(statement.expression);

        if (expression == statement.expression)
            return statement;

        return new BoundExpressionStatement(expression);
    }

    protected virtual BoundStatement RewriteForStatement(BoundForStatement statement) {
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

    protected virtual BoundStatement RewriteWhileStatement(BoundWhileStatement statement) {
        var condition = RewriteExpression(statement.condition);
        var body = RewriteStatement(statement.body);

        if (condition == statement.condition && body == statement.body)
            return statement;

        return new BoundWhileStatement(condition, body, statement.breakLabel, statement.continueLabel);
    }

    protected virtual BoundStatement RewriteIfStatement(BoundIfStatement statement) {
        var condition = RewriteExpression(statement.condition);
        var then = RewriteStatement(statement.then);
        var elseStatement = statement.elseStatement == null ? null : RewriteStatement(statement.elseStatement);

        if (condition == statement.condition && then == statement.then && elseStatement == statement.elseStatement)
            return statement;

        return new BoundIfStatement(condition, then, elseStatement);
    }

    protected virtual BoundStatement RewriteVariableDeclarationStatement(BoundVariableDeclarationStatement statement) {
        var initializer = RewriteExpression(statement.initializer);

        if (initializer == statement.initializer)
            return statement;

        return new BoundVariableDeclarationStatement(statement.variable, initializer);
    }

    protected virtual BoundStatement RewriteBlockStatement(BoundBlockStatement statement) {
        ImmutableArray<BoundStatement>.Builder builder = null;

        for (var i = 0; i < statement.statements.Length; i++) {
            var oldStatement = statement.statements[i];
            var newStatement = RewriteStatement(oldStatement);

            if (newStatement != oldStatement && builder == null) {
                builder = ImmutableArray.CreateBuilder<BoundStatement>(statement.statements.Length);

                for (var j = 0; j < i; j++)
                    builder.Add(statement.statements[j]);
            }

            builder?.Add(newStatement);
        }

        if (builder == null)
            return statement;

        return new BoundBlockStatement(builder.MoveToImmutable());
    }

    internal virtual BoundExpression RewriteExpression(BoundExpression expression) {
        if (expression.constantValue != null)
            return RewriteConstantExpression(expression);

        switch (expression.kind) {
            case BoundNodeKind.BinaryExpression:
                return RewriteBinaryExpression((BoundBinaryExpression)expression);
            case BoundNodeKind.LiteralExpression:
                if (expression is BoundInitializerListExpression il)
                    return RewriteInitializerListExpression(il);
                return RewriteLiteralExpression((BoundLiteralExpression)expression);
            case BoundNodeKind.VariableExpression:
                return RewriteVariableExpression((BoundVariableExpression)expression);
            case BoundNodeKind.AssignmentExpression:
                return RewriteAssignmentExpression((BoundAssignmentExpression)expression);
            case BoundNodeKind.UnaryExpression:
                return RewriteUnaryExpression((BoundUnaryExpression)expression);
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
            case BoundNodeKind.ConstructorExpression:
                return RewriteConstructorExpression((BoundConstructorExpression)expression);
            case BoundNodeKind.MemberAccessExpression:
                return RewriteMemberAccessExpression((BoundMemberAccessExpression)expression);
            case BoundNodeKind.PrefixExpression:
                return RewritePrefixExpression((BoundPrefixExpression)expression);
            case BoundNodeKind.PostfixExpression:
                return RewritePostfixExpression((BoundPostfixExpression)expression);
            default:
                throw new BelteInternalException($"RewriteExpression: unexpected expression type '{expression.kind}'");
        }
    }

    protected virtual BoundExpression RewriteConstantExpression(BoundExpression expression) {
        if (expression.constantValue.value is ImmutableArray<BoundConstant>)
            return new BoundInitializerListExpression(expression.constantValue, expression.type);
        else if (expression is not BoundTypeWrapper)
            return new BoundLiteralExpression(expression.constantValue.value, expression.type);
        else
            return expression;
    }

    protected virtual BoundExpression RewritePrefixExpression(BoundPrefixExpression expression) {
        var operand = RewriteExpression(expression.operand);

        if (operand == expression.operand)
            return expression;

        return new BoundPrefixExpression(expression.op, operand);
    }

    protected virtual BoundExpression RewritePostfixExpression(BoundPostfixExpression expression) {
        var operand = RewriteExpression(expression.operand);

        if (operand == expression.operand)
            return expression;

        return new BoundPostfixExpression(operand, expression.op, expression.isOwnStatement);
    }

    protected virtual BoundExpression RewriteMemberAccessExpression(BoundMemberAccessExpression expression) {
        var operand = RewriteExpression(expression.operand);

        if (operand == expression.operand)
            return expression;

        return new BoundMemberAccessExpression(operand, expression.member, expression.isNullConditional);
    }

    protected virtual BoundExpression RewriteConstructorExpression(BoundConstructorExpression expression) {
        return expression;
    }

    protected virtual BoundExpression RewriteTernaryExpression(BoundTernaryExpression expression) {
        var left = RewriteExpression(expression.left);
        var center = RewriteExpression(expression.center);
        var right = RewriteExpression(expression.right);

        if (left == expression.left && center == expression.center && right == expression.right)
            return expression;

        return new BoundTernaryExpression(left, expression.op, center, right);
    }

    protected virtual BoundExpression RewriteTypeOfExpression(BoundTypeOfExpression expression) {
        return expression;
    }

    protected virtual BoundExpression RewriteReferenceExpression(BoundReferenceExpression expression) {
        return expression;
    }

    protected virtual BoundExpression RewriteIndexExpression(BoundIndexExpression expression) {
        var index = RewriteExpression(expression.index);

        if (index == expression.index)
            return expression;

        return new BoundIndexExpression(expression.operand, index, expression.isNullConditional);
    }

    protected virtual BoundExpression RewriteInitializerListExpression(BoundInitializerListExpression expression) {
        ImmutableArray<BoundExpression>.Builder builder = null;

        for (var i = 0; i < expression.items.Length; i++) {
            var oldItem = expression.items[i];
            var newItem = RewriteExpression(oldItem);

            if (newItem != oldItem) {
                if (builder == null) {
                    builder = ImmutableArray.CreateBuilder<BoundExpression>(expression.items.Length);

                    for (var j = 0; j < i; j++)
                        builder.Add(expression.items[j]);
                }
            }

            builder?.Add(newItem);
        }

        if (builder == null)
            return expression;

        return new BoundInitializerListExpression(builder.MoveToImmutable(), expression.type);
    }

    protected virtual BoundExpression RewriteCompoundAssignmentExpression(
        BoundCompoundAssignmentExpression expression) {
        var left = RewriteExpression(expression.left);
        var right = RewriteExpression(expression.right);

        if (left == expression.left && right == expression.right)
            return expression;

        return new BoundCompoundAssignmentExpression(left, expression.op, right);
    }

    protected virtual BoundExpression RewriteCastExpression(BoundCastExpression expression) {
        var rewrote = RewriteExpression(expression.expression);

        if (rewrote == expression.expression)
            return expression;

        return new BoundCastExpression(expression.type, rewrote);
    }

    protected virtual BoundExpression RewriteCallExpression(BoundCallExpression expression) {
        ImmutableArray<BoundExpression>.Builder builder = null;

        for (var i = 0; i < expression.arguments.Length; i++) {
            var oldArgument = expression.arguments[i];
            var newArgument = RewriteExpression(oldArgument);

            if (newArgument != oldArgument) {
                if (builder == null) {
                    builder = ImmutableArray.CreateBuilder<BoundExpression>(expression.arguments.Length);

                    for (var j = 0; j < i; j++)
                        builder.Add(expression.arguments[j]);
                }
            }

            builder?.Add(newArgument);
        }

        if (builder == null)
            return expression;

        return new BoundCallExpression(expression.method, builder.MoveToImmutable());
    }

    protected virtual BoundExpression RewriteErrorExpression(BoundErrorExpression expression) {
        return expression;
    }

    protected virtual BoundExpression RewriteEmptyExpression(BoundEmptyExpression expression) {
        return expression;
    }

    protected virtual BoundExpression RewriteBinaryExpression(BoundBinaryExpression expression) {
        var left = RewriteExpression(expression.left);
        var right = RewriteExpression(expression.right);

        if (left == expression.left && right == expression.right)
            return expression;

        return new BoundBinaryExpression(left, expression.op, right);
    }

    protected virtual BoundExpression RewriteLiteralExpression(BoundLiteralExpression expression) {
        return expression;
    }

    protected virtual BoundExpression RewriteVariableExpression(BoundVariableExpression expression) {
        return expression;
    }

    protected virtual BoundExpression RewriteAssignmentExpression(BoundAssignmentExpression expression) {
        var left = RewriteExpression(expression.left);
        var right = RewriteExpression(expression.right);

        if (left == expression.left && right == expression.right)
            return expression;

        return new BoundAssignmentExpression(left, right);
    }

    protected virtual BoundExpression RewriteUnaryExpression(BoundUnaryExpression expression) {
        var operand = RewriteExpression(expression.operand);

        if (operand == expression.operand)
            return expression;

        return new BoundUnaryExpression(expression.op, operand);
    }
}
