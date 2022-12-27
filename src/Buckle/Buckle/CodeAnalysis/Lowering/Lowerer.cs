using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Lowering;

/// <summary>
/// Lowers statements to be simpler and use less language features.
/// </summary>
internal sealed class Lowerer : BoundTreeRewriter {
    private int _labelCount;

    private Lowerer() { }

    /// <summary>
    /// Lowers a <see cref="FunctionSymbol" />.
    /// </summary>
    /// <param name="statement">Function body.</param>
    /// <returns>Lowered function body (same type).</returns>
    internal static BoundBlockStatement Lower(FunctionSymbol function, BoundStatement statement) {
        var lowerer = new Lowerer();
        var block = Flatten(function, lowerer.RewriteStatement(statement));
        return RemoveDeadCode(block);
    }

    protected override BoundStatement RewriteIfStatement(BoundIfStatement node) {
        /*

        if <condition>
            <then>

        ---->

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

        ---->

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
        if (node.condition.kind != BoundNodeKind.EmptyExpression)
            condition = node.condition;

        var whileStatement = new BoundWhileStatement(
            condition, whileBody, node.breakLabel, GenerateLabel());

        var result = new BoundBlockStatement(ImmutableArray.Create<BoundStatement>(node.initializer, whileStatement));
        return RewriteStatement(result);
    }

    protected override BoundStatement RewriteConditionalGotoStatement(BoundConditionalGotoStatement statement) {
        /*

        goto <label> if <condition>

        ----> <condition> is constant true

        goto <label>

        ----> <condition> is constant false

        ;

        */
        if (statement.condition.constantValue != null && statement.condition.constantValue.value != null) {
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

        ----> <op> is 'is' and <right> is 'null'

        !HasValue(<left>)

        ----> <op> is 'isnt' and <right> is 'null'

        HasValue(<left>)

        ----> <op> is '??'

        (HasValue(<left>) ? Value(<left>) : <right>)

        ----> <op> is '**'

        {
            int n = <left>;
            for (int i = 1; i < <right>; i+=1)
                n *= <left>;

            return n;
        }

        ----> <left> is nullable and <right> is nullable

        ((HasValue(<left>) && HasValue(<right>)) ? Value(<left>) <op> Value(<right>) : null)

        ----> <left> is nullable

        (HasValue(<left>) ? Value(<left>) <op> <right> : null)

        ----> <right> is nullable

        (<right> isnt null ? <left> <op> Value(<right>) : null)

        */
        if (expression.op.opType == BoundBinaryOperatorKind.Is) {
            var operand = new BoundCallExpression(
                BuiltinFunctions.HasValue, ImmutableArray.Create<BoundExpression>(expression.left)
            );

            var op = BoundUnaryOperator.Bind(SyntaxKind.ExclamationToken, operand.type);

            return RewriteExpression(new BoundUnaryExpression(op, operand));
        }

        if (expression.op.opType == BoundBinaryOperatorKind.Isnt) {
            return RewriteExpression(new BoundCallExpression(
                BuiltinFunctions.HasValue, ImmutableArray.Create<BoundExpression>(expression.left)
            ));
        }

        if (expression.op.opType == BoundBinaryOperatorKind.NullCoalescing) {
            var left = new BoundCallExpression(
                BuiltinFunctions.HasValue, ImmutableArray.Create<BoundExpression>(expression.left)
            );

            var center = new BoundCallExpression(
                CorrectValue(expression.right), ImmutableArray.Create<BoundExpression>(expression.left)
            );

            var op = BoundTernaryOperator.Bind(
                SyntaxKind.QuestionToken, SyntaxKind.ColonToken, left.type,
                center.type, expression.right.type
            );

            return RewriteExpression(new BoundTernaryExpression(left, op, center, expression.right));
        }

        if (expression.op.opType == BoundBinaryOperatorKind.Power) {
            // TODO
            // * Will do in the StackFrameParser
            return base.RewriteBinaryExpression(expression);
        }

        if (expression.left.type.isNullable && expression.right.type.isNullable) {
            var binaryLeft = new BoundCallExpression(
                BuiltinFunctions.HasValue, ImmutableArray.Create<BoundExpression>(expression.left)
            );

            var binaryRight = new BoundCallExpression(
                BuiltinFunctions.HasValue, ImmutableArray.Create<BoundExpression>(expression.right)
            );

            var binaryOp = BoundBinaryOperator.Bind(
                SyntaxKind.AmpersandAmpersandToken, binaryLeft.type, binaryRight.type
            );

            var left = new BoundBinaryExpression(binaryLeft, binaryOp, binaryRight);
            var secondBinaryLeft = new BoundCallExpression(
                CorrectValue(expression.left), ImmutableArray.Create<BoundExpression>(expression.left)
            );

            var secondBinaryRight = new BoundCallExpression(
                CorrectValue(expression.right), ImmutableArray.Create<BoundExpression>(expression.right)
            );

            var center = new BoundBinaryExpression(secondBinaryLeft, expression.op, secondBinaryRight);
            var right = new BoundLiteralExpression(null);
            var op = BoundTernaryOperator.Bind(
                SyntaxKind.QuestionToken, SyntaxKind.ColonToken, left.type, center.type, right.type
            );

            return RewriteExpression(new BoundTernaryExpression(left, op, center, right));
        }

        if (expression.left.type.isNullable) {
            var left = new BoundCallExpression(
                BuiltinFunctions.HasValue, ImmutableArray.Create<BoundExpression>(expression.left)
            );

            var binaryLeft = new BoundCallExpression(
                CorrectValue(expression.left), ImmutableArray.Create<BoundExpression>(expression.left)
            );

            var center = new BoundBinaryExpression(binaryLeft, expression.op, expression.right);
            var right = new BoundLiteralExpression(null);
            var op = BoundTernaryOperator.Bind(
                SyntaxKind.QuestionToken, SyntaxKind.ColonToken, left.type, center.type, right.type
            );

            return RewriteExpression(new BoundTernaryExpression(left, op, center, right));
        }

        if (expression.right.type.isNullable) {
            var left = new BoundCallExpression(
                BuiltinFunctions.HasValue, ImmutableArray.Create<BoundExpression>(expression.right)
            );

            var binaryRight = new BoundCallExpression(
                CorrectValue(expression.left), ImmutableArray.Create<BoundExpression>(expression.right)
            );

            var center = new BoundBinaryExpression(expression.left, expression.op, binaryRight);
            var right = new BoundLiteralExpression(null);
            var op = BoundTernaryOperator.Bind(
                SyntaxKind.QuestionToken, SyntaxKind.ColonToken, left.type, center.type, right.type
            );

            return RewriteExpression(new BoundTernaryExpression(left, op, center, right));
        }

        return base.RewriteBinaryExpression(expression);
    }

    protected override BoundExpression RewriteUnaryExpression(BoundUnaryExpression expression) {
        /*

        <op> <operand>

        ----> <operand> is nullable

        (HasValue(<operand>) ? <op> Value(<operand>) : null)

        */
        if (expression.operand.type.isNullable) {
            var left = new BoundCallExpression(
                BuiltinFunctions.HasValue, ImmutableArray.Create<BoundExpression>(expression.operand)
            );

            var center = new BoundUnaryExpression(
                expression.op, new BoundCallExpression(
                    CorrectValue(expression.operand), ImmutableArray.Create<BoundExpression>(expression.operand)
                )
            );

            var right = new BoundLiteralExpression(null);
            var op = BoundTernaryOperator.Bind(
                SyntaxKind.QuestionToken, SyntaxKind.ColonToken, left.type, center.type, right.type
            );

            return RewriteExpression(new BoundTernaryExpression(left, op, center, right));
        }

        return base.RewriteUnaryExpression(expression);
    }

    protected override BoundExpression RewriteCastExpression(BoundCastExpression expression) {
        /*

        (<type>)<expression>

        ----> <type> is nullable and <expression> is nullable

        (HasValue(<expression>) ? (<type>)Value(<expression>) : null)

        ----> <expression> is nullable

        (<type>)Value(<expression>)

        */
        if (expression.type.isNullable && expression.expression.type.isNullable) {
            var left = new BoundCallExpression(
                BuiltinFunctions.HasValue, ImmutableArray.Create<BoundExpression>(expression.expression)
            );

            var center = new BoundCastExpression(
                expression.type,
                new BoundCallExpression(
                    CorrectValue(expression.expression),
                    ImmutableArray.Create<BoundExpression>(expression.expression)
                )
            );

            var right = new BoundLiteralExpression(null);
            var op = BoundTernaryOperator.Bind(
                SyntaxKind.QuestionToken, SyntaxKind.ColonToken, left.type, center.type, right.type
            );

            return RewriteExpression(new BoundTernaryExpression(left, op, center, right));
        }

        if (expression.expression.type.isNullable) {
            var newExpression = new BoundCallExpression(
                CorrectValue(expression.expression), ImmutableArray.Create<BoundExpression>(expression.expression)
            );

            return base.RewriteCastExpression(new BoundCastExpression(expression.type, newExpression));
        }

        return base.RewriteCastExpression(expression);
    }

    protected override BoundExpression RewriteCallExpression(BoundCallExpression expression) {
        /*

        <function>(<parameters>)

        ---->

        <function>(<parameters>)

        Now parameters do not have compiler generated '$' symbols in their name

        */
        var function = expression.function;
        var parameters = ImmutableArray.CreateBuilder<ParameterSymbol>();

        foreach (var oldParameter in function.parameters) {
            var name = oldParameter.name.StartsWith("$")
                ? oldParameter.name.Substring(1)
                : oldParameter.name;

            var parameter = new ParameterSymbol(name, oldParameter.type, oldParameter.ordinal);
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
            function.name, parameters.ToImmutable(), function.type, function.declaration);

        if (builder == null)
            return new BoundCallExpression(newFunction, expression.arguments);
        else
            return new BoundCallExpression(newFunction, builder.ToImmutable());
    }

    protected override BoundExpression RewriteTernaryExpression(BoundTernaryExpression expression) {
        /*

        <left> <op> <center> <op> <right>

        ----> <op> is '?:' and <left> is constant true

        <center>

        ----> <op> is '?:' and <left> is constant false

        <right>

        */
        if (expression.op.opType == BoundTernaryOperatorKind.Conditional) {
            if (expression.left.constantValue != null && (bool)expression.left.constantValue.value) {
                return RewriteExpression(expression.center);
            }

            if (expression.left.constantValue != null && !(bool)expression.left.constantValue.value) {
                return RewriteExpression(expression.right);
            }
        }

        return base.RewriteTernaryExpression(expression);
    }

    protected override BoundExpression RewriteCompoundAssignmentExpression(
        BoundCompoundAssignmentExpression expression) {
        /*

        <left> <op> <right>

        ---->

        <left> = <left> <simplified op> <right>

        */
        var boundBinaryExpression = new BoundBinaryExpression(expression.left, expression.op, expression.right);

        return RewriteAssignmentExpression(new BoundAssignmentExpression(expression.left, boundBinaryExpression));
    }

    protected override BoundExpression RewriteMemberAccessExpression(BoundMemberAccessExpression expression) {
        /*

        <operand><op><member>

        ----> <op> is '?.'

        (HasValue(<operand>) ? <operand>.<member> : null)

        */
        if (expression.isNullConditional) {
            var left = new BoundCallExpression(
                BuiltinFunctions.HasValue, ImmutableArray.Create<BoundExpression>(expression.operand)
            );

            var center = new BoundMemberAccessExpression(expression.operand, expression.member, false);
            var right = new BoundLiteralExpression(null);
            var op = BoundTernaryOperator.Bind(
                SyntaxKind.QuestionToken, SyntaxKind.ColonToken, left.type, center.type, right.type
            );

            return RewriteExpression(new BoundTernaryExpression(left, op, center, right));
        }

        return base.RewriteMemberAccessExpression(expression);
    }

    private FunctionSymbol CorrectValue(BoundExpression expression) {
        if (expression.type.typeSymbol == TypeSymbol.Bool)
            return BuiltinFunctions.ValueBool;
        if (expression.type.typeSymbol == TypeSymbol.Decimal)
            return BuiltinFunctions.ValueDecimal;
        if (expression.type.typeSymbol == TypeSymbol.Int)
            return BuiltinFunctions.ValueInt;
        if (expression.type.typeSymbol == TypeSymbol.String)
            return BuiltinFunctions.ValueString;

        return BuiltinFunctions.ValueAny;
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

        if (function.type.typeSymbol == TypeSymbol.Void)
            if (builder.Count == 0 || CanFallThrough(builder.Last()))
                builder.Add(new BoundReturnStatement(null));

        return new BoundBlockStatement(builder.ToImmutable());
    }

    private static bool CanFallThrough(BoundStatement boundStatement) {
        return boundStatement.kind != BoundNodeKind.ReturnStatement &&
            boundStatement.kind != BoundNodeKind.GotoStatement;
    }

    private BoundLabel GenerateLabel() {
        var name = $"Label{++_labelCount}";
        return new BoundLabel(name);
    }
}
