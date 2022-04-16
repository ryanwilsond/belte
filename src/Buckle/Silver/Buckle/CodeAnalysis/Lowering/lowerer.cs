using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Lowering {
    internal sealed class Lowerer : BoundTreeRewriter {
        private int labelCount_;

        private Lowerer() { }

        private BoundLabel GenerateLabel() {
            var name = $"Label{++labelCount_}";
            return new BoundLabel(name);
        }

        public static BoundBlockStatement Lower(FunctionSymbol function, BoundStatement statement) {
            var lowerer = new Lowerer();
            return Flatten(function, lowerer.RewriteStatement(statement));
        }

        private static BoundBlockStatement Flatten(FunctionSymbol function, BoundStatement statement) {
            var builder = ImmutableArray.CreateBuilder<BoundStatement>();
            var stack = new Stack<BoundStatement>();
            stack.Push(statement);

            while (stack.Count > 0) {
                var current = stack.Pop();

                if (current is BoundBlockStatement block) {
                    foreach (var s in block.statements.Reverse())
                        stack.Push(s);
                } else {
                    builder.Add(current);
                }
            }

            if (function.lType == TypeSymbol.Void)
                if (builder.Count == 0 || CanFallThrough(builder.Last()))
                    builder.Add(new BoundReturnStatement(null));

            return new BoundBlockStatement(builder.ToImmutable());
        }

        private static bool CanFallThrough(BoundStatement boundStatement) {
            // TODO: rewrite conditional gotos with guaranteed result of condition into nothing or unconditional goto
            return boundStatement.type != BoundNodeType.ReturnStatement &&
                boundStatement.type != BoundNodeType.GotoStatement;
        }

        protected override BoundStatement RewriteIfStatement(BoundIfStatement node) {
            /*
            if <condition>
                <then>

            --->

            gotoFalse <condition> end
            <then>
            end:

            ==============================

            if <condition>
                <then>
            else
                <elseStatement>

            ---->

            gotoFalse <condition> else
            <then>
            goto end
            else:
            <elseStatement>
            end:
            */
            if (node.elseStatement == null) {
                var endLabel = GenerateLabel();
                var gotoFalse = new BoundConditionalGotoStatement(endLabel, node.condition, false);
                var endLabelStatement = new BoundLabelStatement(endLabel);
                var result = new BoundBlockStatement(
                    ImmutableArray.Create<BoundStatement>(gotoFalse, node.then, endLabelStatement));

                return RewriteStatement(result);
            } else {
                var elseLabel = GenerateLabel();
                var endLabel = GenerateLabel();
                var gotoFalse = new BoundConditionalGotoStatement(elseLabel, node.condition, false);
                var gotoEnd = new BoundGotoStatement(endLabel);
                var elseLabelStatement = new BoundLabelStatement(elseLabel);
                var endLabelStatement = new BoundLabelStatement(endLabel);
                var result = new BoundBlockStatement(ImmutableArray.Create<BoundStatement>(
                    gotoFalse, node.then, gotoEnd, elseLabelStatement, node.elseStatement, endLabelStatement)
                );

                return RewriteStatement(result);
            }
        }

        protected override BoundStatement RewriteWhileStatement(BoundWhileStatement node) {
            /*
            while <condition>
                <body>

            ---->

            continue:
            gotoFalse <condition> end
            <body>
            goto check
            break:
            */
            var continueLabel = node.continueLabel;
            var breakLabel = node.breakLabel;
            var gotoFalse = new BoundConditionalGotoStatement(breakLabel, node.condition, false);
            var gotoCheck = new BoundGotoStatement(continueLabel);
            var continueLabelStatement = new BoundLabelStatement(continueLabel);
            var breakLabelStatement = new BoundLabelStatement(breakLabel);
            var result = new BoundBlockStatement(ImmutableArray.Create<BoundStatement>(
                continueLabelStatement, gotoFalse, node.body, gotoCheck, breakLabelStatement
            ));

            return RewriteStatement(result);
        }

        protected override BoundStatement RewriteDoWhileStatement(BoundDoWhileStatement node) {
            /*
            do
                <body>
            while <condition>

            ---->

            continue:
            <body>
            gotoTrue <condition> continue
            break:
            */
            var continueLabel = node.continueLabel;
            var breakLabel = node.breakLabel;
            var continueLabelStatement = new BoundLabelStatement(continueLabel);
            var breakLabelStatement = new BoundLabelStatement(breakLabel);
            var gotoTrue = new BoundConditionalGotoStatement(continueLabel, node.condition);

            var result = new BoundBlockStatement(ImmutableArray.Create<BoundStatement>(
                continueLabelStatement, node.body, gotoTrue, breakLabelStatement
            ));

            return RewriteStatement(result);
        }

        protected override BoundStatement RewriteForStatement(BoundForStatement node) {
            /*
            for (<initializer> <condition>; <step>)
                <body>

            --->

            {
                <initializer>
                while (<condition>) {
                    <body>
                continue:
                    <step>;
                }
            }
            */
            var step = new BoundExpressionStatement(node.step);
            var continueLabelStatement = new BoundLabelStatement(node.continueLabel);
            var breakLabelStatement = new BoundLabelStatement(node.breakLabel);

            var whileBody = new BoundBlockStatement(ImmutableArray.Create<BoundStatement>(
                node.body, continueLabelStatement, step
            ));

            BoundExpression condition = new BoundLiteralExpression(true);
            if (node.condition.type != BoundNodeType.EmptyExpression)
                condition = node.condition;

            var whileStatement = new BoundWhileStatement(
                condition, whileBody, node.breakLabel, GenerateLabel());

            var result = new BoundBlockStatement(ImmutableArray.Create<BoundStatement>(node.initializer, whileStatement));
            return RewriteStatement(result);
        }
    }
}
