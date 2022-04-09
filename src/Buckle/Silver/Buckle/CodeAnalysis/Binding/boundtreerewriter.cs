using System;
using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Binding {

    internal abstract class BoundTreeRewriter {
        public virtual BoundStatement RewriteStatement(BoundStatement statement) {
            switch (statement.type) {
                case BoundNodeType.BlockStatement:
                    return RewriteBlockStatement((BoundBlockStatement)statement);
                case BoundNodeType.VariableDeclarationStatement:
                    return RewriteVariableDeclarationStatement((BoundVariableDeclarationStatement)statement);
                case BoundNodeType.IfStatement:
                    return RewriteIfStatement((BoundIfStatement)statement);
                case BoundNodeType.WhileStatement:
                    return RewriteWhileStatement((BoundWhileStatement)statement);
                case BoundNodeType.ForStatement:
                    return RewriteForStatement((BoundForStatement)statement);
                case BoundNodeType.ExpressionStatement:
                    return RewriteExpressionStatement((BoundExpressionStatement)statement);
                case BoundNodeType.LabelStatement:
                    return RewriteLabelStatement((BoundLabelStatement)statement);
                case BoundNodeType.GotoStatement:
                    return RewriteGotoStatement((BoundGotoStatement)statement);
                case BoundNodeType.ConditionalGotoStatement:
                    return RewriteConditionalGotoStatement((BoundConditionalGotoStatement)statement);
                case BoundNodeType.DoWhileStatement:
                    return RewriteDoWhileStatement((BoundDoWhileStatement)statement);
                case BoundNodeType.ReturnStatement:
                    return RewriteReturnStatement((BoundReturnStatement)statement);
                default: return null;
            }
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

            return new BoundConditionalGotoStatement(statement.label, condition);
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
                step == statement.step && body == statement.body)
                return statement;

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

            for (int i = 0; i < statement.statements.Length; i++) {
                var oldStatement = statement.statements[i];
                var newStatement = RewriteStatement(oldStatement);

                if (newStatement != oldStatement && builder == null) {
                    builder = ImmutableArray.CreateBuilder<BoundStatement>(statement.statements.Length);

                    for (int j = 0; j < i; j++)
                        builder.Add(statement.statements[j]);
                }

                if (builder != null)
                    builder.Add(newStatement);
            }

            if (builder == null)
                return statement;

            return new BoundBlockStatement(builder.MoveToImmutable());
        }

        public virtual BoundExpression RewriteExpression(BoundExpression expression) {
            switch (expression.type) {
                case BoundNodeType.BinaryExpression:
                    return RewriteBinaryExpression((BoundBinaryExpression)expression);
                case BoundNodeType.LiteralExpression:
                    return RewriteLiteralExpression((BoundLiteralExpression)expression);
                case BoundNodeType.VariableExpression:
                    return RewriteVariableExpression((BoundVariableExpression)expression);
                case BoundNodeType.AssignmentExpression:
                    return RewriteAssignmentExpression((BoundAssignmentExpression)expression);
                case BoundNodeType.UnaryExpression:
                    return RewriteUnaryExpression((BoundUnaryExpression)expression);
                case BoundNodeType.EmptyExpression:
                    return RewriteEmptyExpression((BoundEmptyExpression)expression);
                case BoundNodeType.ErrorExpression:
                    return RewriteErrorExpression((BoundErrorExpression)expression);
                case BoundNodeType.CallExpression:
                    return RewriteCallExpression((BoundCallExpression)expression);
                case BoundNodeType.CastExpression:
                    return RewriteCastExpression((BoundCastExpression)expression);
                default: return null;
            }
        }

        protected virtual BoundExpression RewriteCastExpression(BoundCastExpression expression) {
            var rewrote = RewriteExpression(expression.expression);
            if (rewrote == expression.expression)
                return expression;

            return new BoundCastExpression(expression.lType, rewrote);
        }

        protected virtual BoundExpression RewriteCallExpression(BoundCallExpression expression) {
            ImmutableArray<BoundExpression>.Builder builder = null;

            for (int i=0; i<expression.arguments.Length; i++) {
                var oldArgument = expression.arguments[i];
                var newArgument = RewriteExpression(oldArgument);
                if (newArgument != oldArgument) {
                    if (builder == null) {
                        builder = ImmutableArray.CreateBuilder<BoundExpression>(expression.arguments.Length);

                        for (int j=0; j<i; j++)
                            builder.Add(expression.arguments[j]);
                    }
                }

                if (builder != null)
                    builder.Add(newArgument);
            }

            if (builder == null)
                return expression;

            return new BoundCallExpression(expression.function, builder.MoveToImmutable());
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
            var rewritten = RewriteExpression(expression.expression);
            if (rewritten == expression.expression)
                return expression;

            return new BoundAssignmentExpression(expression.variable, rewritten);
        }

        protected virtual BoundExpression RewriteUnaryExpression(BoundUnaryExpression expression) {
            var operand = RewriteExpression(expression.operand);
            if (operand == expression.operand)
                return expression;

            return new BoundUnaryExpression(expression.op, operand);
        }
    }
}
