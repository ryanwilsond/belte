using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Immutable;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Lowering;

internal sealed class Lowerer : BoundTreeRewriter {
    private int labelCount_;
    private int inlineFunctionCount_;
    private ImmutableDictionary<FunctionSymbol, BoundBlockStatement>.Builder functionBodies_;

    private Lowerer(ImmutableDictionary<FunctionSymbol, BoundBlockStatement>.Builder functionBodies) {
        functionBodies_ = functionBodies;
    }

    private BoundLabel GenerateLabel() {
        var name = $"Label{++labelCount_}";
        return new BoundLabel(name);
    }

    private FunctionSymbol GenerateFunction(BoundTypeClause returnType) {
        var name = $"$Inline{++inlineFunctionCount_}";
        return new FunctionSymbol(name, ImmutableArray<ParameterSymbol>.Empty, returnType);
    }

    public static BoundBlockStatement Lower(
        FunctionSymbol function, BoundStatement statement,
        ImmutableDictionary<FunctionSymbol, BoundBlockStatement>.Builder functionBodies) {
        var lowerer = new Lowerer(functionBodies);
        var block = Flatten(function, lowerer.RewriteStatement(statement));
        return RemoveDeadCode(block);
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

    protected override BoundExpression RewriteBinaryExpression(BoundBinaryExpression expression) {
        /*
        <left> <op> <right>

        ---> <left> is nullable

        {
            <type> left0 = null;
            if (<left> != null) {
                [NotNull]<type> left1 = ([NotNull]<type>)<left>;
                left0 = left1 <op> <right>;
            }
            return left0;
        }

        ---> <right> is nullable

        {
            <type> right0 = null;
            if (<right> != null) {
                [NotNull]<type> right1 = ([NotNull]<type>)<right>;
                right0 = <left> <op> right1;
            }
            return right0;
        }

        ---> <left> and <right> are nullable

        {
            <type> left0 = null;
            if (<left> != null && <right> != null) {
                [NotNull]<type> left1 = ([NotNull]<type>)<left>;
                [NotNull]<type> right0 = ([NotNull]<type>)<right>;
                left0 = left1 <op> right0;
            }
            return left0;
        }

        ---> <op> is <, >, <=, or >=

        {
            [NotNull]bool left0 = false;
            if (<left> != null && <right> != null) {
                [NotNull]<type> left1 = ([NotNull]<type>)<left>;
                [NotNull]<type> right0 = ([NotNull]<type>)<right>;
                left0 = left1 <op> right0;
            }

            return left0;
        }

        ---> <op> is **

        {
            int <n> = <left>;
            for (int i = 1; i < <right>; i+=1)
                <n> *= <left>; // this will rewrite again to account for null

            return <n>;
        }

        */
        var left = RewriteExpression(expression.left);
        var right = RewriteExpression(expression.right);

        // equality are special cases that the emitter handles
        if (expression.op.opType == BoundBinaryOperatorType.EqualityEquals ||
            expression.op.opType == BoundBinaryOperatorType.EqualityNotEquals) {
            if (left == expression.left && right == expression.right)
                return expression;
            else
                return new BoundBinaryExpression(left, expression.op, right);
        }

        // TODO: make sure that when rewriting again it doesnt loop back here infinitely

        // <, >, <=, >= rewrite to return false if <left> or <right> are null
        // all other operators return null if <left> or <right> are null

        return new BoundBinaryExpression(left, expression.op, right);
    }

    protected override BoundExpression RewriteUnaryExpression(BoundUnaryExpression expression) {
        /*
        <op> <operand>

        ---> <operand> is nullable

        {
            <type> operand0 = null;
            if (<operand> != null) {
                [NotNull]<type> operand1 = ([NotNull]<type>)<operand>;
                operand0 = <op> operand1;
            }
            return operand0;
        }

        */
        // TODO: make sure that when rewriting again it doesnt loop back here infinitely
        // * Solve this by lowering in the binder, also need to do that because inlines need to lower during binding

        var operand = RewriteExpression(expression.operand);
        if (operand == expression.operand)
            return expression;

        return new BoundUnaryExpression(expression.op, operand);
    }

    protected override BoundExpression RewriteInlineFunctionExpression(BoundInlineFunctionExpression expression) {
        /*
        ...<body>...

        --->

        <returnType> inline0() <body>

        ...inline0()...

        */
        // TODO: need to actually lower inline functions during the binding process
        // to allow an immediate call to BindLocalFunctionDeclaration

        var inlineFunction = GenerateFunction(expression.returnType);
        var rewrittenBody = (BoundBlockStatement)RewriteBlockStatement(expression.body);
        rewrittenBody = Flatten(inlineFunction, rewrittenBody);
        functionBodies_.Add(inlineFunction, rewrittenBody);

        return RewriteExpression(new BoundCallExpression(inlineFunction, ImmutableArray<BoundExpression>.Empty));
    }

    protected override BoundExpression RewriteCastExpression(BoundCastExpression expression) {
        /*

        (<type>)<expression>

        ---> <type> and <expression> is nullable

        {
            <expressionType> expression0 = null;
            if (<expression> != null) {
                [NotNull]<expressionType> expression1 = ([NotNull]<expressionType>)<expression>;
                expression0 = (<type>)expression1;
            }
            return expression0;
        }

        */
        // * Note that this code doesnt loop because only rewrite when both types are nullable
        // TODO emit implicit casts to use the conv instruction
        var rewrote = RewriteExpression(expression.expression);
        if (rewrote == expression.expression)
            return expression;

        return new BoundCastExpression(expression.typeClause, rewrote);
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
}
