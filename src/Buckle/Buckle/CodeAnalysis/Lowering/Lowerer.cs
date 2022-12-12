using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Immutable;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Lowering;

/// <summary>
/// Lowers statements to be simpler and use less language features.
/// </summary>
internal sealed class Lowerer : BoundTreeRewriter {
    private int _labelCount;
    private int _inlineFunctionCount;

    private Lowerer() { }

    /// <summary>
    /// Lowers a function.
    /// </summary>
    /// <param name="function">Function symbol</param>
    /// <param name="statement">Function body</param>
    /// <returns>Lowered function body (same type)</returns>
    internal static BoundBlockStatement Lower(FunctionSymbol function, BoundStatement statement) {
        var lowerer = new Lowerer();
        var block = Flatten(function, lowerer.RewriteStatement(statement));
        return RemoveDeadCode(block);
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
            var result = new BoundBlockStatement(ImmutableArray.Create<BoundStatement>(
                gotoFalse, node.then, endLabelStatement
            ));

            return RewriteStatement(result);
        } else {
            var elseLabel = GenerateLabel();
            var endLabel = GenerateLabel();
            var gotoFalse = new BoundConditionalGotoStatement(elseLabel, node.condition, false);
            var gotoEnd = new BoundGotoStatement(endLabel);
            var elseLabelStatement = new BoundLabelStatement(elseLabel);
            var endLabelStatement = new BoundLabelStatement(endLabel);
            var result = new BoundBlockStatement(ImmutableArray.Create<BoundStatement>(
                gotoFalse, node.then, gotoEnd, elseLabelStatement, node.elseStatement, endLabelStatement
            ));

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
        goto continue
        break:
        */
        var continueLabel = node.continueLabel;
        var breakLabel = node.breakLabel;
        var gotoFalse = new BoundConditionalGotoStatement(breakLabel, node.condition, false);
        var gotoContinue = new BoundGotoStatement(continueLabel);
        var continueLabelStatement = new BoundLabelStatement(continueLabel);
        var breakLabelStatement = new BoundLabelStatement(breakLabel);
        var result = new BoundBlockStatement(ImmutableArray.Create<BoundStatement>(
            continueLabelStatement, gotoFalse, node.body, gotoContinue, breakLabelStatement
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

    protected override BoundStatement RewriteConditionalGotoStatement(BoundConditionalGotoStatement statement) {
        if (statement.condition.constantValue != null) {
            var condition = (bool)statement.condition.constantValue.value;
            condition = statement.jumpIfTrue ? condition : !condition;

            if (condition)
                return RewriteStatement(new BoundGotoStatement(statement.label));
            else
                return RewriteStatement(new BoundNopStatement());
        }

        return base.RewriteConditionalGotoStatement(statement);
    }

    protected override BoundExpression RewriteCallExpression(BoundCallExpression expression) {
        var function = expression.function;
        var parameters = ImmutableArray.CreateBuilder<ParameterSymbol>();

        foreach (var oldParameter in function.parameters) {
            var name = oldParameter.name.StartsWith("$")
                ? oldParameter.name.Substring(1)
                : oldParameter.name;

            var parameter = new ParameterSymbol(name, oldParameter.typeClause, oldParameter.ordinal);
            parameters.Add(parameter);
        }

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

        var newFunction = new FunctionSymbol(
            function.name, parameters.ToImmutable(), function.typeClause, function.declaration);

        if (builder == null)
            return new BoundCallExpression(newFunction, expression.arguments);
        else
            return new BoundCallExpression(newFunction, builder.ToImmutable());
    }


    private static BoundBlockStatement RemoveDeadCode(BoundBlockStatement statement) {
        var controlFlow = ControlFlowGraph.Create(statement);
        var reachableStatements = new HashSet<BoundStatement>(controlFlow.blocks.SelectMany(b => b.statements));

        var builder = statement.statements.ToBuilder();
        for (int i=builder.Count-1; i>=0; i--) {
            if (!reachableStatements.Contains(builder[i]))
                builder.RemoveAt(i);
        }

        return new BoundBlockStatement(builder.ToImmutable());
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

        if (function.typeClause.lType == TypeSymbol.Void)
            if (builder.Count == 0 || CanFallThrough(builder.Last()))
                builder.Add(new BoundReturnStatement(null));

        return new BoundBlockStatement(builder.ToImmutable());
    }

    private static bool CanFallThrough(BoundStatement boundStatement) {
        return boundStatement.type != BoundNodeType.ReturnStatement &&
            boundStatement.type != BoundNodeType.GotoStatement;
    }

    private BoundLabel GenerateLabel() {
        var name = $"Label{++_labelCount}";
        return new BoundLabel(name);
    }

    private FunctionSymbol GenerateFunction(BoundTypeClause returnType) {
        var name = $"$Inline{++_inlineFunctionCount}";
        return new FunctionSymbol(name, ImmutableArray<ParameterSymbol>.Empty, returnType);
    }
}
