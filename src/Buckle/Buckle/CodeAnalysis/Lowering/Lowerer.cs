using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using static Buckle.CodeAnalysis.Binding.BoundFactory;

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

            return RewriteStatement(
                Block(
                    GotoIfNot(
                        @goto: endLabel,
                        @ifNot: node.condition
                    ),
                    node.then,
                    Label(endLabel)
                )
            );
        } else {
            var elseLabel = GenerateLabel();
            var endLabel = GenerateLabel();

            return RewriteStatement(
                Block(
                    GotoIfNot(
                        @goto: elseLabel,
                        @ifNot: node.condition
                    ),
                    node.then,
                    Goto(endLabel),
                    Label(elseLabel),
                    node.elseStatement,
                    Label(endLabel)
                )
            );
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

        return RewriteStatement(
            Block(
                Label(continueLabel),
                GotoIfNot(
                    @goto: breakLabel,
                    @ifNot: node.condition
                ),
                node.body,
                Goto(continueLabel),
                Label(breakLabel)
            )
        );
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

        return RewriteStatement(
            Block(
                Label(continueLabel),
                node.body,
                GotoIf(
                    @goto: continueLabel,
                    @if: node.condition
                ),
                Label(breakLabel)
            )
        );
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
        var continueLabel = node.continueLabel;
        var breakLabel = node.breakLabel;
        var condition = node.condition.kind == BoundNodeKind.EmptyExpression
            ? Literal(true)
            : node.condition;

        return RewriteStatement(
            Block(
                node.initializer,
                While(
                    condition,
                    Block(
                        node.body,
                        Label(continueLabel),
                        Statement(node.step)
                    ),
                    breakLabel,
                    GenerateLabel()
                )
            )
        );
    }

    protected override BoundStatement RewriteConditionalGotoStatement(BoundConditionalGotoStatement statement) {
        /*

        goto <label> if <condition>

        ----> <condition> is constant true

        goto <label>

        ----> <condition> is constant false

        ;

        */
        if (BoundConstant.IsNotNull(statement.condition.constantValue)) {
            var condition = (bool)statement.condition.constantValue.value;
            condition = statement.jumpIfTrue ? condition : !condition;

            if (condition)
                return RewriteStatement(Goto(statement.label));
            else
                return RewriteStatement(Nop());
        }

        return base.RewriteConditionalGotoStatement(statement);
    }

    protected override BoundExpression RewriteBinaryExpression(BoundBinaryExpression expression) {
        /*

        <left> <op> <right>

        ----> <op> is 'is' and <right> is 'null'

        (!HasValue(<left>))

        ----> <op> is 'isnt' and <right> is 'null'

        (HasValue(<left>))

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
        if (expression.op.opKind == BoundBinaryOperatorKind.Is)
            return RewriteExpression(Not(HasValue(expression.left)));

        if (expression.op.opKind == BoundBinaryOperatorKind.Isnt)
            return RewriteExpression(HasValue(expression.left));

        if (expression.op.opKind == BoundBinaryOperatorKind.NullCoalescing) {
            return RewriteExpression(
                NullConditional(
                    @if: HasValue(expression.left),
                    @then: Value(expression.left),
                    @else: expression.right
                )
            );
        }

        if (expression.op.opKind == BoundBinaryOperatorKind.Power) {
            // TODO
            // * Will do in the Blender
            return base.RewriteBinaryExpression(expression);
        }

        if (expression.left.type.isNullable && expression.right.type.isNullable) {
            return RewriteExpression(
                NullConditional(
                    @if: And(
                        HasValue(expression.left),
                        HasValue(expression.right)
                    ),
                    @then: Binary(
                        Value(expression.left),
                        expression.op,
                        Value(expression.right)
                    ),
                    @else: Literal(null)
                )
            );
        }

        if (expression.left.type.isNullable) {
            return RewriteExpression(
                NullConditional(
                    @if: HasValue(expression.left),
                    @then: Binary(
                        Value(expression.left),
                        expression.op,
                        expression.right
                    ),
                    @else: Literal(null)
                )
            );
        }

        if (expression.right.type.isNullable) {
            return RewriteExpression(
                NullConditional(
                    @if: HasValue(expression.right),
                    @then: Binary(
                        expression.left,
                        expression.op,
                        Value(expression.right)
                    ),
                    @else: Literal(null)
                )
            );
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
            return RewriteExpression(
                NullConditional(
                    @if: HasValue(expression.operand),
                    @then: Unary(
                        expression.op,
                        Value(expression.operand)
                    ),
                    @else: Literal(null)
                )
            );
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
            return RewriteExpression(
                NullConditional(
                    @if: HasValue(expression.expression),
                    @then: Cast(
                        expression.type,
                        Value(expression.expression)
                    ),
                    @else: Literal(null)
                )
            );
        }

        if (expression.expression.type.isNullable) {
            return base.RewriteCastExpression(
                Cast(
                    expression.type,
                    Value(expression.expression)
                )
            );
        }

        return base.RewriteCastExpression(expression);
    }

    protected override BoundExpression RewriteCallExpression(BoundCallExpression expression) {
        /*

        <function>(<parameters>)

        ---->

        (<function>(<parameters>))

        Now parameters do not have compiler generated '$' symbols in their name

        ----> <function> is 'Value' and <parameter> is not nullable

        <parameter>

        ----> <function> is 'HasValue' and <parameter> is not nullable

        true

        */
        var function = expression.function;
        var parameters = ImmutableArray.CreateBuilder<ParameterSymbol>();

        if (function.name == "Value" && !expression.arguments[0].type.isNullable)
            return RewriteExpression(expression.arguments[0]);
        else if (function.name == "HasValue" && !expression.arguments[0].type.isNullable)
            return new BoundLiteralExpression(true);

        foreach (var oldParameter in function.parameters) {
            var name = oldParameter.name.StartsWith("$")
                ? oldParameter.name.Substring(1)
                : oldParameter.name;

            var parameter = new ParameterSymbol(
                name, oldParameter.type, oldParameter.ordinal, oldParameter.defaultValue
            );

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
            function.name, parameters.ToImmutable(), function.type, function.declaration
        );

        if (builder == null)
            return base.RewriteCallExpression(new BoundCallExpression(newFunction, expression.arguments));
        else
            return base.RewriteCallExpression(new BoundCallExpression(newFunction, builder.ToImmutable()));
    }

    protected override BoundExpression RewriteTernaryExpression(BoundTernaryExpression expression) {
        /*

        <left> <op> <center> <op> <right>

        ---->

        if (<left>) {
            <center>
        } else {
            <right>
        }

        ----> <op> is '?:' and <left> is constant true

        (<center>)

        ----> <op> is '?:' and <left> is constant false

       (<right>)

        */
        if (expression.op.opKind == BoundTernaryOperatorKind.Conditional) {
            if (BoundConstant.IsNotNull(expression.left.constantValue) && (bool)expression.left.constantValue.value)
                return RewriteExpression(expression.center);

            if (BoundConstant.IsNotNull(expression.left.constantValue) && !(bool)expression.left.constantValue.value)
                return RewriteExpression(expression.right);

            // TODO
        }

        return base.RewriteTernaryExpression(expression);
    }

    protected override BoundExpression RewriteCompoundAssignmentExpression(
        BoundCompoundAssignmentExpression expression) {
        /*

        <left> <op>= <right>

        ---->

        (<left> = <left> <op> <right>)

        */
        return RewriteExpression(
            Assignment(
                expression.left,
                Binary(
                    expression.left,
                    expression.op,
                    expression.right
                )
            )
        );
    }

    protected override BoundExpression RewriteMemberAccessExpression(BoundMemberAccessExpression expression) {
        /*

        <operand><op><member>

        ----> <op> is '?.'

        (HasValue(<operand>) ? <operand>.<member> : null)

        */
        if (expression.isNullConditional) {
            return RewriteExpression(
                NullConditional(
                    @if: HasValue(expression.operand),
                    @then: MemberAccess(
                        expression.operand,
                        expression.member
                    ),
                    @else: Literal(null)
                )
            );
        }

        return base.RewriteMemberAccessExpression(expression);
    }

    protected override BoundExpression RewriteIndexExpression(BoundIndexExpression expression) {
        /*

        <operand><openBracket><index>]

        ----> <openBracket> is '?['

        (HasValue(<operand>) ? <operand>[<index>] : null)

        */
        if (expression.isNullConditional) {
            return RewriteExpression(
                NullConditional(
                    @if: HasValue(expression.operand),
                    @then: Index(
                        expression.operand,
                        expression.index
                    ),
                    @else: Literal(null)
                )
            );
        }

        return base.RewriteIndexExpression(expression);
    }

    protected override BoundExpression RewritePrefixExpression(BoundPrefixExpression expression) {
        /*

        <op><operand>

        ----> <op> is '++'

        (<operand> += 1)

        ----> <op> is '--'

        (<operand> -= 1)

        */
        if (expression.op.opKind == BoundPrefixOperatorKind.Increment)
            return RewriteExpression(Increment(expression.operand));
        else
            return RewriteExpression(Decrement(expression.operand));
    }

    protected override BoundExpression RewritePostfixExpression(BoundPostfixExpression expression) {
        /*

        <operand><op>

        ----> <op> is '!'

        (Value(<operand>))

        ----> <op> is '++' and <isOwnStatement>

        (<operand> += 1)

        ----> <op> is '++'

        ((<operand> += 1) - 1)

        ----> <op> is '--' and <isOwnStatement>

        (<operand> -= 1)

        ----> <op> is '--'

        ((<operand> -= 1) + 1)

        */
        if (expression.op.opKind == BoundPostfixOperatorKind.NullAssert)
            return RewriteExpression(Value(expression.operand));

        var assignment = expression.op.opKind == BoundPostfixOperatorKind.Increment
            ? Increment(expression.operand)
            : Decrement(expression.operand);

        if (expression.isOwnStatement) {
            return RewriteExpression(assignment);
        } else {
            var reversal = expression.op.opKind == BoundPostfixOperatorKind.Increment
                ? Subtract(assignment, Literal(1))
                : Add(assignment, Literal(1));

            return RewriteExpression(reversal);
        }
    }

    private BoundExpression Value(BoundExpression expression) {
        if (expression.type.typeSymbol == TypeSymbol.Bool)
            return Call(BuiltinFunctions.ValueBool, expression);
        if (expression.type.typeSymbol == TypeSymbol.Decimal)
            return Call(BuiltinFunctions.ValueDecimal, expression);
        if (expression.type.typeSymbol == TypeSymbol.Int)
            return Call(BuiltinFunctions.ValueInt, expression);
        if (expression.type.typeSymbol == TypeSymbol.String)
            return Call(BuiltinFunctions.ValueString, expression);

        return Cast(
            expression.type,
            Call(
                BuiltinFunctions.ValueAny,
                expression
            )
        );
    }

    private BoundExpression HasValue(BoundExpression expression) {
        if (expression.type.typeSymbol == TypeSymbol.Bool)
            return Call(BuiltinFunctions.HasValueBool, expression);
        if (expression.type.typeSymbol == TypeSymbol.Decimal)
            return Call(BuiltinFunctions.HasValueDecimal, expression);
        if (expression.type.typeSymbol == TypeSymbol.Int)
            return Call(BuiltinFunctions.HasValueInt, expression);
        if (expression.type.typeSymbol == TypeSymbol.String)
            return Call(BuiltinFunctions.HasValueString, expression);

        return Call(BuiltinFunctions.HasValueAny, expression);
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

        if (function.type.typeSymbol == TypeSymbol.Void) {
            if (builder.Count == 0 || CanFallThrough(builder.Last()))
                builder.Add(new BoundReturnStatement(null));
        }

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
