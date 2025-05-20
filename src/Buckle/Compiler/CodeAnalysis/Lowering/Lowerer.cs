using System;
using System.Collections.Generic;
using System.Linq;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Diagnostics;
using Buckle.Libraries;
using Microsoft.CodeAnalysis.PooledObjects;
using static Buckle.CodeAnalysis.Binding.BoundFactory;

namespace Buckle.CodeAnalysis.Lowering;

/// <summary>
/// Lowers statements to be simpler and use less language features.
/// </summary>
internal sealed class Lowerer : BoundTreeRewriter {
    private readonly Expander _expander;
    private int _labelCount;

    private Lowerer(MethodSymbol container) {
        _expander = new Expander(container);
    }

    internal static BoundBlockStatement Lower(
        MethodSymbol method,
        BoundStatement statement,
        BelteDiagnosticQueue diagnostics) {
        var lowerer = new Lowerer(method);

        // TODO Maybe separate Optimizer into Optimizer and DeadCodeRemover to prevent calling the Optimizer twice?
        var optimizedStatement = Optimizer.Optimize(statement, false, diagnostics);
        var expandedStatement = lowerer._expander.Expand(optimizedStatement);
        var rewrittenStatement = (BoundStatement)lowerer.Visit(expandedStatement);
        var block = Flatten(method, rewrittenStatement);
        var optimizedBlock = Optimizer.Optimize(block, true, diagnostics) as BoundBlockStatement;

        return optimizedBlock;
    }

    internal override BoundNode Visit(BoundNode node) {
        if (node is null)
            return null;

        if (node is BoundExpression e && e.constantValue is not null)
            return VisitConstant(e);

        return base.Visit(node);
    }

    private BoundNode VisitConstant(BoundExpression expression) {
        // TODO Handle initializer list constants
        var type = expression.type;
        var literal = new BoundLiteralExpression(expression.syntax, expression.constantValue, type);

        if (!type.IsNullableType() ||
            type.GetNullableUnderlyingType().specialType == SpecialType.String ||
            expression.constantValue.value is null) {
            return literal;
        } else {
            return new BoundObjectCreationExpression(
                expression.syntax,
                CorLibrary.GetWellKnownMember(WellKnownMembers.Nullable_ctor)
                    .Construct([new TypeOrConstant(type.GetNullableUnderlyingType())]),
                [literal],
                default,
                default,
                default,
                false,
                type
            );
        }
    }

    internal override BoundNode VisitIfStatement(BoundIfStatement statement) {
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
        var syntax = statement.syntax;

        if (statement.alternative is null) {
            var endLabel = GenerateLabel();

            return VisitBlockStatement(
                Block(syntax,
                    GotoIfNot(syntax,
                        @goto: endLabel,
                        @ifNot: statement.condition
                    ),
                    statement.consequence,
                    Label(syntax, endLabel)
                )
            );
        } else {
            var elseLabel = GenerateLabel();
            var endLabel = GenerateLabel();

            return VisitBlockStatement(
                Block(syntax,
                    GotoIfNot(syntax,
                        @goto: elseLabel,
                        @ifNot: statement.condition
                    ),
                    statement.consequence,
                    Goto(syntax, endLabel),
                    Label(syntax, elseLabel),
                    statement.alternative,
                    Label(syntax, endLabel)
                )
            );
        }
    }

    internal override BoundNode VisitWhileStatement(BoundWhileStatement statement) {
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
        var syntax = statement.syntax;
        var continueLabel = statement.continueLabel;
        var breakLabel = statement.breakLabel;

        return VisitBlockStatement(
            Block(syntax,
                statement.locals,
                Label(syntax, continueLabel),
                GotoIfNot(syntax,
                    @goto: breakLabel,
                    @ifNot: statement.condition
                ),
                statement.body,
                Goto(syntax, continueLabel),
                Label(syntax, breakLabel)
            )
        );
    }

    internal override BoundNode VisitDoWhileStatement(BoundDoWhileStatement statement) {
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
        var syntax = statement.syntax;
        var continueLabel = statement.continueLabel;
        var breakLabel = statement.breakLabel;

        return VisitBlockStatement(
            Block(syntax,
                statement.locals,
                Label(syntax, continueLabel),
                statement.body,
                GotoIf(syntax,
                    @goto: continueLabel,
                    @if: statement.condition
                ),
                Label(syntax, breakLabel)
            )
        );
    }

    internal override BoundNode VisitForStatement(BoundForStatement statement) {
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
        var syntax = statement.syntax;
        var continueLabel = statement.continueLabel;
        var breakLabel = statement.breakLabel;
        // var condition = statement.condition.kind == BoundKind.EmptyExpression
        //     ? Literal(syntax, true)
        //     : statement.condition;
        var condition = statement.condition;

        return Visit(
            _expander.Expand(
                Block(syntax,
                    statement.locals,
                    statement.initializer,
                    While(syntax,
                        statement.innerLocals,
                        condition,
                        Block(syntax,
                            statement.body,
                            Label(syntax, continueLabel),
                            Statement(syntax, statement.step)
                        ),
                        breakLabel,
                        GenerateLabel()
                    )
                )
            )
        );
    }

    internal override BoundNode VisitBreakStatement(BoundBreakStatement statement) {
        /*

        break;

        ---->

        goto <label>

        */
        return Goto(statement.syntax, statement.label);
    }

    internal override BoundNode VisitContinueStatement(BoundContinueStatement statement) {
        /*

        continue;

        ---->

        goto <label>

        */
        return Goto(statement.syntax, statement.label);
    }

    internal override BoundNode VisitBinaryOperator(BoundBinaryOperator expression) {
        /*

        <left> <op> <right>

        ----> <op> has a method attached

        <method>(<left>, <right>)

        ----> <left> is nullable and <right> is nullable

        ((HasValue(<left>) && HasValue(<right>)) ? Value(<left>) <op> Value(<right>) : null)

        ----> <left> is nullable

        (HasValue(<left>) ? Value(<left>) <op> <right> : null)

        ----> <right> is nullable

        (<right> isnt null ? <left> <op> Value(<right>) : null)

        */
        var syntax = expression.syntax;
        var op = expression.operatorKind;

        if (expression.method is not null)
            return Visit(Call(syntax, expression.method, expression.left, expression.right));

        if (op.IsLifted()) {
            if (expression.left.type.IsNullableType() &&
                expression.right.type.IsNullableType() &&
                expression.left.constantValue is null &&
                expression.right.constantValue is null) {
                return VisitConditionalOperator(
                    Conditional(syntax,
                        @if: And(syntax,
                            HasValue(syntax, expression.left),
                            HasValue(syntax, expression.right)
                        ),
                        @then: Binary(syntax,
                            Value(syntax, expression.left, expression.left.type.GetNullableUnderlyingType()),
                            op,
                            Value(syntax, expression.right, expression.right.type.GetNullableUnderlyingType()),
                            expression.type
                        ),
                        @else: Literal(syntax, null, expression.type),
                        expression.type
                    )
                );
            }

            if (expression.left.type.IsNullableType() && expression.left.constantValue is null) {
                return VisitConditionalOperator(
                    Conditional(syntax,
                        @if: HasValue(syntax, expression.left),
                        @then: Binary(syntax,
                            Value(syntax, expression.left, expression.left.type.GetNullableUnderlyingType()),
                            op,
                            expression.right,
                            expression.type
                        ),
                        @else: Literal(syntax, null, expression.type),
                        expression.type
                    )
                );
            }

            if (expression.right.type.IsNullableType() && expression.right.constantValue is null) {
                return VisitConditionalOperator(
                    Conditional(syntax,
                        @if: HasValue(syntax, expression.right),
                        @then: Binary(syntax,
                            expression.left,
                            op,
                            Value(syntax, expression.right, expression.right.type.GetNullableUnderlyingType()),
                            expression.type
                        ),
                        @else: Literal(syntax, null, expression.type),
                        expression.type
                    )
                );
            }
        }

        return base.VisitBinaryOperator(expression);
    }

    internal override BoundNode VisitNullCoalescingOperator(BoundNullCoalescingOperator expression) {
        /*

        <left> ?? <right>

        ---->

        (HasValue(<left>) ? Value(<left>) : <right>)

        */
        var syntax = expression.syntax;

        return VisitConditionalOperator(
            Conditional(syntax,
                @if: HasValue(syntax, expression.left),
                @then: Value(syntax, expression.left, expression.left.type),
                @else: expression.right,
                expression.type
            )
        );
    }

    internal override BoundNode VisitUnaryOperator(BoundUnaryOperator expression) {
        /*

        <op> <operand>

        ----> <op> has a method attached

        <method>(<op>)

        ----> <op> is +

        <operand>

        ----> <operand> is nullable

        (HasValue(<operand>) ? <op> Value(<operand>) : null)

        */
        var syntax = expression.syntax;
        var op = expression.operatorKind;

        if (expression.method is not null)
            return Visit(Call(syntax, expression.method, expression.operand));

        if (op == UnaryOperatorKind.UnaryPlus)
            return Visit(expression.operand);

        // TODO Is there any case where an operator is lifted but not nullable or vice versa?
        if (op.IsLifted() && expression.operand.type.IsNullableType()) {
            return VisitConditionalOperator(
                Conditional(syntax,
                    @if: HasValue(syntax, expression.operand),
                    @then: Unary(syntax,
                        op,
                        Value(syntax, expression.operand, expression.operand.type.GetNullableUnderlyingType()),
                        expression.type
                    ),
                    @else: Literal(syntax, null, expression.type),
                    expression.type
                )
            );
        }

        return base.VisitUnaryOperator(expression);
    }

    internal override BoundNode VisitIncrementOperator(BoundIncrementOperator expression) {
        /*

        <op> <operand>

        ----> <op> has a method attached

        <method>(<op>)

        ----> <op> is '++'

        <operand> += 1

        ----> <op> is '--'

        <operand> -= 1

        */
        var syntax = expression.syntax;
        var op = expression.operatorKind.Operator();

        if (expression.method is not null)
            return Visit(Call(syntax, expression.method, expression.operand));

        if (op is UnaryOperatorKind.PrefixIncrement or UnaryOperatorKind.PostfixIncrement)
            return Visit(Increment(syntax, expression.operand));
        else
            return Visit(Decrement(syntax, expression.operand));
    }

    internal override BoundNode VisitIsOperator(BoundIsOperator expression) {
        /*

        <left> is <right>

        ----> <right> is null

        <left>.get_HasValue()

        */
        var syntax = expression.syntax;

        if (expression.right.IsLiteralNull()) {
            var call = InstanceCall(
                syntax,
                expression.left,
                CorLibrary.GetWellKnownMember(WellKnownMembers.Nullable_getHasValue)
                    .Construct([new TypeOrConstant(expression.left.type.GetNullableUnderlyingType())])
            );

            if (expression.isNot)
                return Visit(call);

            return Visit(Unary(syntax, UnaryOperatorKind.BoolLogicalNegation, call, call.type));
        }

        return base.VisitIsOperator(expression);
    }

    internal override BoundNode VisitNullAssertOperator(BoundNullAssertOperator expression) {
        /*

        <operand>!

        ---->

        <operand>.get_Value

        */
        var syntax = expression.syntax;

        return Visit(InstanceCall(
            syntax,
            expression.operand,
            CorLibrary.GetWellKnownMember(WellKnownMembers.Nullable_getValue)
                .Construct([new TypeOrConstant(expression.type)])
        ));
    }

    internal override BoundNode VisitCastExpression(BoundCastExpression expression) {
        /*

        (<type>)<expression>

        ----> <expression> is nullable and <type> is not nullable

        (<type>)Value(<expression>)

        ----> <expression> is nullable and <type> is not nullable and <expression>.type and <type> are otherwise equal

        Value(<expression>)

        ----> <expression> is not nullable and <type> is nullable and <expression>.type and <type> are otherwise equal

        <expression>

        */
        var syntax = expression.syntax;
        var operand = expression.operand;
        var type = expression.type;
        var operandType = operand.type;

        if (operandType.IsNullableType() && !type.IsNullableType()) {
            if (type.Equals(operandType))
                return Visit(Value(syntax, operand, operandType.GetNullableUnderlyingType()));

            return base.VisitCastExpression(
                Cast(syntax,
                    type,
                    Value(syntax, operand, operandType.GetNullableUnderlyingType()),
                    Conversion.ExplicitNullable,
                    operand.constantValue
                )
            );
        }

        if (operandType?.Equals(type) ?? false)
            return Visit(operand);

        return base.VisitCastExpression(expression);
    }

    internal override BoundNode VisitCallExpression(BoundCallExpression expression) {
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
        var syntax = expression.syntax;
        var method = expression.method;
        // var parameters = ArrayBuilder<ParameterSymbol>.GetInstance();

        if (method.name == "Value" && !expression.arguments[0].type.IsNullableType())
            return Visit(expression.arguments[0]);
        else if (method.name == "HasValue" && !expression.arguments[0].type.IsNullableType())
            return Literal(syntax, true, expression.type);

        /*
        var parametersChanged = false;

        foreach (var oldParameter in method.parameters) {
            var name = oldParameter.name.StartsWith("$")
                ? oldParameter.name.Substring(1)
                : oldParameter.name;

            if (name == oldParameter.name) {
                parameters.Add(oldParameter);
                continue;
            }

            TODO Check if we even need this
            var parameter = new ParameterSymbol(
                name, oldParameter.type, oldParameter.ordinal, oldParameter.defaultValue
            );

            parametersChanged = true;
            parameters.Add(parameter);
        }
        */

        ArrayBuilder<BoundExpression> builder = null;

        for (var i = 0; i < expression.arguments.Length; i++) {
            var oldArgument = expression.arguments[i];
            var newArgument = (BoundExpression)Visit(oldArgument);

            if (newArgument != oldArgument) {
                if (builder is null) {
                    builder = ArrayBuilder<BoundExpression>.GetInstance(expression.arguments.Length);

                    for (var j = 0; j < i; j++)
                        builder.Add(expression.arguments[j]);
                }
            }

            builder?.Add(newArgument);
        }

        // var newMethod = parametersChanged
        //     ? method.UpdateParameters(parameters.ToImmutableAndFree())
        //     : method;

        var arguments = builder is null ? expression.arguments : builder.ToImmutableAndFree();

        return base.VisitCallExpression(
            new BoundCallExpression(
                syntax,
                expression.receiver,
                method,
                arguments,
                expression.argumentRefKinds,
                expression.defaultArguments,
                expression.resultKind,
                expression.type
            )
        );
    }

    internal override BoundNode VisitCompoundAssignmentOperator(BoundCompoundAssignmentOperator expression) {
        /*

        <left> <op>= <right>

        ---->

        <left> = <left> <op> <right>

        */
        var syntax = expression.syntax;

        return VisitAssignmentOperator(
            Assignment(syntax,
                expression.left,
                Binary(syntax,
                    expression.left,
                    expression.op.kind,
                    expression.right,
                    expression.type
                ),
                false,
                expression.type
            )
        );
    }

    internal override BoundNode VisitNullCoalescingAssignmentOperator(BoundNullCoalescingAssignmentOperator expression) {
        /*

        <left> ??= <right>

        ---->

        <left> = <left> ?? <right>

        */
        var syntax = expression.syntax;

        return VisitAssignmentOperator(
            Assignment(syntax,
                expression.left,
                new BoundNullCoalescingOperator(
                    syntax,
                    expression.left,
                    expression.right,
                    null,
                    expression.type
                ),
                false,
                expression.type
            )
        );
    }

    internal static BoundBlockStatement Flatten(MethodSymbol method, BoundStatement statement) {
        var syntax = statement.syntax;
        var statementsBuilder = ArrayBuilder<BoundStatement>.GetInstance();
        var localsBuilder = ArrayBuilder<DataContainerSymbol>.GetInstance();
        var functionsBuilder = ArrayBuilder<LocalFunctionSymbol>.GetInstance();

        var stack = new Stack<BoundStatement>();
        stack.Push(statement);

        while (stack.Count > 0) {
            var current = stack.Pop();

            if (current is BoundBlockStatement block) {
                localsBuilder.AddRange(block.locals);
                functionsBuilder.AddRange(block.localFunctions);

                foreach (var s in block.statements.Reverse())
                    stack.Push(s);
            } else {
                statementsBuilder.Add(current);
            }
        }

        if (method.returnsVoid) {
            if (statementsBuilder.Count == 0 || CanFallThrough(statementsBuilder.Last()))
                statementsBuilder.Add(new BoundReturnStatement(syntax, RefKind.None, null));
        }

        return new BoundBlockStatement(
            syntax,
            statementsBuilder.ToImmutableAndFree(),
            localsBuilder.ToImmutableAndFree(),
            functionsBuilder.ToImmutableAndFree()
        );
    }

    private static bool CanFallThrough(BoundStatement boundStatement) {
        return boundStatement.kind != BoundKind.ReturnStatement &&
            boundStatement.kind != BoundKind.GotoStatement;
    }

    private SynthesizedLabelSymbol GenerateLabel() {
        return new SynthesizedLabelSymbol($"Label{++_labelCount}");
    }
}
