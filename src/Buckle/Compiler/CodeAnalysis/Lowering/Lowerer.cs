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
    private readonly bool _transpilerMode;

    private int _labelCount;

    private Lowerer(bool transpilerMode) {
        _transpilerMode = transpilerMode;
    }

    /// <summary>
    /// Lowers a <see cref="MethodSymbol" />.
    /// </summary>
    /// <param name="method">Method to lower.</param>
    /// <param name="statement">Method body.</param>
    /// <param name="transpilerMode">If the compiler is transpiling, if true skips part of lowering.</param>
    /// <returns>Lowered method body (same type).</returns>
    internal static BoundBlockStatement Lower(MethodSymbol method, BoundStatement statement, bool transpilerMode) {
        var expandedStatement = Expander.Expand(statement);
        var lowerer = new Lowerer(transpilerMode);
        var block = Flatten(method, lowerer.RewriteStatement(expandedStatement));

        return Optimizer.Optimize(block, transpilerMode) as BoundBlockStatement;
    }

    protected override BoundStatement RewriteIfStatement(BoundIfStatement statement) {
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
        if (_transpilerMode)
            return base.RewriteIfStatement(statement);

        if (statement.elseStatement is null) {
            var endLabel = GenerateLabel();

            return RewriteStatement(
                Block(
                    GotoIfNot(
                        @goto: endLabel,
                        @ifNot: statement.condition
                    ),
                    statement.then,
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
                        @ifNot: statement.condition
                    ),
                    statement.then,
                    Goto(endLabel),
                    Label(elseLabel),
                    statement.elseStatement,
                    Label(endLabel)
                )
            );
        }
    }

    protected override BoundStatement RewriteWhileStatement(BoundWhileStatement statement) {
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
        if (_transpilerMode)
            return base.RewriteWhileStatement(statement);

        var continueLabel = statement.continueLabel;
        var breakLabel = statement.breakLabel;

        return RewriteStatement(
            Block(
                Label(continueLabel),
                GotoIfNot(
                    @goto: breakLabel,
                    @ifNot: statement.condition
                ),
                statement.body,
                Goto(continueLabel),
                Label(breakLabel)
            )
        );
    }

    protected override BoundStatement RewriteDoWhileStatement(BoundDoWhileStatement statement) {
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
        if (_transpilerMode)
            return base.RewriteDoWhileStatement(statement);

        var continueLabel = statement.continueLabel;
        var breakLabel = statement.breakLabel;

        return RewriteStatement(
            Block(
                Label(continueLabel),
                statement.body,
                GotoIf(
                    @goto: continueLabel,
                    @if: statement.condition
                ),
                Label(breakLabel)
            )
        );
    }

    protected override BoundStatement RewriteForStatement(BoundForStatement statement) {
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
        if (_transpilerMode)
            return base.RewriteForStatement(statement);

        var continueLabel = statement.continueLabel;
        var breakLabel = statement.breakLabel;
        var condition = statement.condition.kind == BoundNodeKind.EmptyExpression
            ? Literal(true)
            : statement.condition;

        return RewriteStatement(
            Expander.Expand(
                Block(
                    statement.initializer,
                    While(
                        condition,
                        Block(
                            statement.body,
                            Label(continueLabel),
                            Statement(statement.step)
                        ),
                        breakLabel,
                        GenerateLabel()
                    )
                )
            )
        );
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

        if (expression.op.opKind == BoundBinaryOperatorKind.Power) {
            // TODO
            return base.RewriteBinaryExpression(expression);
        }

        if (expression.op.opKind == BoundBinaryOperatorKind.NullCoalescing) {
            return RewriteExpression(
                NullConditional(
                    @if: HasValue(expression.left),
                    @then: Value(expression.left),
                    @else: expression.right
                )
            );
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
                    @else: Literal(null, expression.type)
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
                    @else: Literal(null, expression.type)
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
                    @else: Literal(null, expression.type)
                )
            );
        }

        return base.RewriteBinaryExpression(expression);
    }

    protected override BoundExpression RewriteUnaryExpression(BoundUnaryExpression expression) {
        /*

        <op> <operand>

        ----> <op> is +

        <operand>

        ----> <operand> is nullable

        (HasValue(<operand>) ? <op> Value(<operand>) : null)

        */
        if (expression.op.opKind == BoundUnaryOperatorKind.NumericalIdentity)
            return RewriteExpression(expression.operand);

        if (expression.operand.type.isNullable) {
            return RewriteExpression(
                NullConditional(
                    @if: HasValue(expression.operand),
                    @then: Unary(
                        expression.op,
                        Value(expression.operand)
                    ),
                    @else: Literal(null, expression.type)
                )
            );
        }

        return base.RewriteUnaryExpression(expression);
    }

    protected override BoundExpression RewriteCastExpression(BoundCastExpression expression) {
        /*

        (<type>)<expression>

        ----> <expression> is nullable

        (<type>)Value(<expression>)

        ----> <expression> is not nullable and <type> is nullable and <expression>.type and <type> are otherwise equal

        */

        if (expression.expression.type.isNullable && !expression.type.isNullable) {
            return base.RewriteCastExpression(
                Cast(
                    expression.type,
                    Value(expression.expression)
                )
            );
        }

        if (BoundType.CopyWith(expression.expression.type, isNullable: true).Equals(expression.type, true))
            return RewriteExpression(expression.expression);

        return base.RewriteCastExpression(expression);
    }

    protected override BoundExpression RewriteCallExpression(BoundCallExpression expression) {
        /*

        <method>(<parameters>)

        ---->

        (<method>(<parameters>))

        Now parameters do not have compiler generated '$' symbols in their name

        ----> <method> is 'Value' and <parameter> is not nullable

        <parameter>

        ----> <method> is 'HasValue' and <parameter> is not nullable

        true

        ----> is static access

        (<method>(<parameters>))

        Method operand rewritten to exclude TypeOf expression

        */
        var method = expression.method;
        var parameters = ImmutableArray.CreateBuilder<ParameterSymbol>();

        if (method.name == "Value" && !expression.arguments[0].type.isNullable)
            return RewriteExpression(expression.arguments[0]);
        else if (method.name == "HasValue" && !expression.arguments[0].type.isNullable)
            return new BoundLiteralExpression(true);

        var parametersChanged = false;

        foreach (var oldParameter in method.parameters) {
            var name = oldParameter.name.StartsWith("$")
                ? oldParameter.name.Substring(1)
                : oldParameter.name;

            if (name == oldParameter.name) {
                parameters.Add(oldParameter);
                continue;
            }

            var parameter = new ParameterSymbol(
                name, oldParameter.type, oldParameter.ordinal, oldParameter.defaultValue
            );

            parametersChanged = true;
            parameters.Add(parameter);
        }

        ImmutableArray<BoundExpression>.Builder builder = null;

        for (var i = 0; i < expression.arguments.Length; i++) {
            var oldArgument = expression.arguments[i];
            var newArgument = RewriteExpression(oldArgument);

            if (newArgument != oldArgument) {
                if (builder is null) {
                    builder = ImmutableArray.CreateBuilder<BoundExpression>(expression.arguments.Length);

                    for (var j = 0; j < i; j++)
                        builder.Add(expression.arguments[j]);
                }
            }

            builder?.Add(newArgument);
        }

        var newMethod = parametersChanged
            ? method.UpdateParameters(parameters.ToImmutable())
            : method;

        var arguments = builder is null ? expression.arguments : builder.ToImmutable();
        var operand = (expression.expression is BoundMemberAccessExpression me && me.isStaticAccess)
            ? new BoundEmptyExpression()
            : expression.expression;

        return base.RewriteCallExpression(new BoundCallExpression(operand, newMethod, arguments));
    }

    protected override BoundExpression RewriteCompoundAssignmentExpression(
        BoundCompoundAssignmentExpression expression) {
        /*

        <left> <op>= <right>

        ---->

        (<left> = <left> <op> <right>)

        */
        if (_transpilerMode)
            return base.RewriteCompoundAssignmentExpression(expression);

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

        ----> is static access

        <member>

        */
        if (expression.isNullConditional) {
            return RewriteExpression(
                NullConditional(
                    @if: HasValue(expression.left),
                    @then: MemberAccess(
                        expression.left,
                        expression.right,
                        expression.isStaticAccess
                    ),
                    @else: Literal(null, expression.type)
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
                    @if: HasValue(expression.expression),
                    @then: Index(
                        expression.expression,
                        expression.index
                    ),
                    @else: Literal(null, expression.type)
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
        if (_transpilerMode)
            return base.RewritePrefixExpression(expression);

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

        if (_transpilerMode)
            return base.RewritePostfixExpression(expression);

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
            return Call(BuiltinMethods.ValueBool, expression);
        if (expression.type.typeSymbol == TypeSymbol.Decimal)
            return Call(BuiltinMethods.ValueDecimal, expression);
        if (expression.type.typeSymbol == TypeSymbol.Int)
            return Call(BuiltinMethods.ValueInt, expression);
        if (expression.type.typeSymbol == TypeSymbol.String)
            return Call(BuiltinMethods.ValueString, expression);

        return Cast(
            expression.type,
            Call(
                BuiltinMethods.ValueAny,
                expression
            )
        );
    }

    private BoundExpression HasValue(BoundExpression expression) {
        if (expression.type.typeSymbol == TypeSymbol.Bool)
            return Call(BuiltinMethods.HasValueBool, expression);
        if (expression.type.typeSymbol == TypeSymbol.Decimal)
            return Call(BuiltinMethods.HasValueDecimal, expression);
        if (expression.type.typeSymbol == TypeSymbol.Int)
            return Call(BuiltinMethods.HasValueInt, expression);
        if (expression.type.typeSymbol == TypeSymbol.String)
            return Call(BuiltinMethods.HasValueString, expression);

        return Call(BuiltinMethods.HasValueAny, expression);
    }

    private static BoundBlockStatement Flatten(MethodSymbol method, BoundStatement statement) {
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

        if (method.type.typeSymbol == TypeSymbol.Void) {
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
