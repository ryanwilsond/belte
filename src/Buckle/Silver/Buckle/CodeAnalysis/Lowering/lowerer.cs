using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Buckle.CodeAnalysis.Binding;

namespace Buckle.CodeAnalysis.Lowering {
    internal sealed class Lowerer : BoundTreeRewriter {
        private int labelCount_;

        private Lowerer() { }

        private BoundLabel GenerateLabel() {
            var name = $"Label{++labelCount_}";
            return new BoundLabel(name);
        }

        public static BoundBlockStatement Lower(ImmutableArray<BoundStatement> statements) {
            var lowerer = new Lowerer();
            var builder = ImmutableArray.CreateBuilder<BoundStatement>();
            foreach (var statement in statements)
                builder.Add(lowerer.RewriteStatement(statement));

            return Flatten(builder.ToImmutable());
        }

        private static BoundBlockStatement Flatten(ImmutableArray<BoundStatement> statements) {
            var builder = ImmutableArray.CreateBuilder<BoundStatement>();
            var stack = new Stack<BoundStatement>();
            foreach (var statement in statements.Reverse())
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

            return new BoundBlockStatement(builder.ToImmutable());
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

            check:
            gotoFalse <condition> end
            <body>
            goto check
            end:
            */
            var checkLabel = GenerateLabel();
            var endLabel = GenerateLabel();
            var gotoFalse = new BoundConditionalGotoStatement(endLabel, node.condition, false);
            var gotoCheck = new BoundGotoStatement(checkLabel);
            var checkLabelStatement = new BoundLabelStatement(checkLabel);
            var endLabelStatement = new BoundLabelStatement(endLabel);
            var result = new BoundBlockStatement(ImmutableArray.Create<BoundStatement>(
                checkLabelStatement, gotoFalse, node.body, gotoCheck, endLabelStatement
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
                    <step>;
                }
            }
            */
            var step = new BoundExpressionStatement(node.step);

            var whileBody = new BoundBlockStatement(ImmutableArray.Create<BoundStatement>(node.body, step));
            BoundExpression condition = new BoundLiteralExpression(true);
            if (node.condition.type != BoundNodeType.EmptyExpression)
                condition = node.condition;

            var whileStatement = new BoundWhileStatement(condition, whileBody);

            var result = new BoundBlockStatement(ImmutableArray.Create<BoundStatement>(node.initializer, whileStatement));
            return RewriteStatement(result);
        }
    }
}
