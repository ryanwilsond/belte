using System;
using System.Collections.Generic;
using System.Linq;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.CodeGeneration;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
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

    private Lowerer(MethodSymbol container) {
        _expander = new Expander(container);
    }

    internal static BoundBlockStatement Lower(
        MethodSymbol method,
        BoundStatement statement,
        BelteDiagnosticQueue diagnostics) {
        var lowerer = new Lowerer(method);

        // TODO Maybe separate Optimizer into Optimizer and DeadCodeRemover to prevent calling the Optimizer twice?

        var rewrittenStatement = Optimizer.Optimize(statement, false, diagnostics);
        // We need to lower control of flow before expanding to ensure the condition expressions aren't "cached"
        rewrittenStatement = FlowLowerer.Lower(rewrittenStatement);
        rewrittenStatement = lowerer._expander.Expand(rewrittenStatement);
        rewrittenStatement = (BoundStatement)lowerer.Visit(rewrittenStatement);
        rewrittenStatement = Flatten(method, rewrittenStatement);
        rewrittenStatement = Optimizer.Optimize(rewrittenStatement, true, diagnostics);

        return (BoundBlockStatement)rewrittenStatement;
    }

    internal override BoundNode Visit(BoundNode node) {
        if (node is null)
            return null;

        if (node is BoundExpression e && e.constantValue is not null)
            return VisitConstant(e);

        return base.Visit(node);
    }

    private bool ShouldBeTreatedAsNullable(TypeSymbol type) {
        return type.IsNullableType() && CodeGenerator.IsValueType(type.GetNullableUnderlyingType());
    }

    private BoundExpression CreateNullable(
        SyntaxNode syntax,
        BoundExpression expression,
        TypeSymbol nullableType) {
        if (!ShouldBeTreatedAsNullable(nullableType))
            return expression;

        return new BoundObjectCreationExpression(
            syntax,
            CorLibrary.GetWellKnownMember(WellKnownMembers.Nullable_ctor)
                .Construct([new TypeOrConstant(nullableType.GetNullableUnderlyingType())]),
            [expression],
            default,
            default,
            default,
            false,
            nullableType
        );
    }

    private BoundNode VisitConstant(BoundExpression expression) {
        // TODO Handle initializer list constants
        var syntax = expression.syntax;
        var type = expression.type;

        if (expression.constantValue.value is null)
            type = CorLibrary.GetOrCreateNullableType(type);

        return new BoundLiteralExpression(
            syntax,
            expression.constantValue,
            ShouldBeTreatedAsNullable(type) ? type : type.StrippedType()
        );
    }

    private BoundExpression DeNull(BoundExpression expression) {
        if (expression.constantValue is null)
            return expression;

        return new BoundLiteralExpression(expression.syntax, expression.constantValue, expression.type.StrippedType());
    }

    internal override BoundNode VisitLocalDeclarationStatement(BoundLocalDeclarationStatement statement) {
        /*

        <type> <localSymbol> = <initializer>

        ----> <localSymbol> is nullable and <initializer> is not nullable

        <type> <localSymbol> = new Nullable(<initializer>);

        */
        var declaration = statement.declaration;

        if (declaration.dataContainer.type.IsNullableType() &&
            !declaration.initializer.type.IsNullableType() &&
            CodeGenerator.IsValueType(declaration.initializer.type)) {
            var syntax = statement.syntax;
            return VisitLocalDeclarationStatement(new BoundLocalDeclarationStatement(syntax,
                new BoundDataContainerDeclaration(syntax,
                    declaration.dataContainer,
                    CreateNullable(
                        syntax,
                        declaration.initializer,
                        CorLibrary.GetOrCreateNullableType(declaration.initializer.type)
                    )
                )
            ));
        }

        return base.VisitLocalDeclarationStatement(statement);
    }

    internal override BoundNode VisitConditionalGotoStatement(BoundConditionalGotoStatement statement) {
        /*

        goto <label> if <condition>

        ----> <condition> is conditional operator 'C' and C.falseExpr is <null>

        goto <label> if (<C.condition> ? <C.trueExpr>! : LowLevel.ThrowNullConditionException())

        ----> <condition> is nullable

        goto <label> if <condition>.get_Value()

        */
        var condition = (BoundExpression)Visit(statement.condition);

        if (condition.constantValue is null &&
            condition.type.IsNullableType()) {
            var syntax = statement.syntax;

            if (condition is BoundConditionalOperator conditional) {
                condition = Conditional(
                    syntax,
                    conditional.condition,
                    RewriteNull(syntax, conditional.trueExpression),
                    RewriteNull(syntax, conditional.falseExpression),
                    CorLibrary.GetSpecialType(SpecialType.Bool)
                );
            }

            return VisitConditionalGotoStatement(
                new BoundConditionalGotoStatement(
                    syntax,
                    statement.label,
                    RewriteNull(syntax, condition),
                    statement.jumpIfTrue
                )
            );
        }

        return base.VisitConditionalGotoStatement(statement);

        static BoundExpression RewriteNull(SyntaxNode syntax, BoundExpression expression) {
            if (ConstantValue.IsNull(expression.constantValue)) {
                return Call(
                    syntax,
                    (MethodSymbol)StandardLibrary.LowLevel.GetMembers("ThrowNullConditionException")[0],
                    []
                );
            }

            if (expression is BoundObjectCreationExpression creation &&
                creation.type.specialType == SpecialType.Nullable) {
                return RewriteNull(syntax, creation.arguments[0]);
            }

            if (expression is BoundBinaryOperator binary && binary.operatorKind.IsConditional()) {
                return Binary(
                    syntax,
                    RewriteNull(syntax, binary.left),
                    binary.operatorKind,
                    RewriteNull(syntax, binary.right),
                    binary.type.StrippedType()
                );
            }

            if (expression.type.IsNullableType())
                return Value(syntax, expression, expression.type.GetNullableUnderlyingType());

            return expression;
        }
    }

    internal override BoundNode VisitBinaryOperator(BoundBinaryOperator expression) {
        /*

        <left> <op> <right>

        ----> <op> has a method attached

        <method>(<left>, <right>)

        ----> <left> is nullable and <right> is nullable

        ((HasValue(<left>) && HasValue(<right>)) ? new Nullable( Value(<left>) <op> Value(<right>) ) : null)

        ----> <left> is nullable

        (HasValue(<left>) ? new Nullable( Value(<left>) <op> <right> ) : null)

        ----> <right> is nullable

        (<right> isnt null ? new Nullable( <left> <op> Value(<right>) ) : null)

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
                        @then: CreateNullable(syntax,
                            Binary(syntax,
                                Value(syntax, expression.left, expression.left.type.GetNullableUnderlyingType()),
                                op,
                                Value(syntax, expression.right, expression.right.type.GetNullableUnderlyingType()),
                                expression.type.StrippedType()
                                ),
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
                        @then: CreateNullable(syntax,
                            Binary(syntax,
                                Value(syntax, expression.left, expression.left.type.GetNullableUnderlyingType()),
                                op,
                                DeNull(expression.right),
                                expression.type.StrippedType()
                            ),
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
                        @then: CreateNullable(syntax,
                            Binary(syntax,
                                DeNull(expression.left),
                                op,
                                Value(syntax, expression.right, expression.right.type.GetNullableUnderlyingType()),
                                expression.type.StrippedType()
                            ),
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

        (HasValue(<left>) ? <left> : <right>)

        */
        var syntax = expression.syntax;

        return VisitConditionalOperator(
            Conditional(syntax,
                @if: HasValue(syntax, expression.left),
                @then: expression.left,
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

        (HasValue(<operand>) ? new Nullable( <op> Value(<operand>) ) : null)

        */
        var syntax = expression.syntax;
        var op = expression.operatorKind;

        if (expression.method is not null)
            return Visit(Call(syntax, expression.method, expression.operand));

        if (op == UnaryOperatorKind.UnaryPlus)
            return Visit(expression.operand);

        if (op.IsLifted() && expression.operand.type.IsNullableType()) {
            return VisitConditionalOperator(
                Conditional(syntax,
                    @if: HasValue(syntax, expression.operand),
                    @then: CreateNullable(syntax,
                        Unary(syntax,
                            op,
                            Value(syntax, expression.operand, expression.operand.type.GetNullableUnderlyingType()),
                            expression.type.StrippedType()
                        ),
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
        // TODO Flatten null checks:
        /*

        Current:

        a + b + c

        -->

        temp0 = (a isnt null && b isnt null ? a! + b! : null)
        temp1 = (temp0 isnt null && c isnt null ? temp0! + c! : null)

        TODO Lower to:

        temp0 = (a isnt null && b isnt null && c isnt null ? a! + b! + c! : null)

        */

        /*

        <left> is <right>

        ----> <right> is null

        <left>.get_HasValue()

        */
        var syntax = expression.syntax;

        if (expression.right.IsLiteralNull()) {
            if (ShouldBeTreatedAsNullable(expression.left.type)) {
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

            var binaryOp = expression.isNot ? BinaryOperatorKind.NotEqual : BinaryOperatorKind.Equal;
            var right = Literal(expression.right.syntax, null, expression.left.type);

            binaryOp |= expression.left.type.specialType == SpecialType.String
                ? BinaryOperatorKind.String
                : BinaryOperatorKind.Object;

            return Visit(Binary(syntax, expression.left, binaryOp, right, expression.type));
        }

        return base.VisitIsOperator(expression);
    }

    internal override BoundNode VisitNullAssertOperator(BoundNullAssertOperator expression) {
        /*

        <operand>!

        ---->

        <operand>.get_Value

        */
        if (ShouldBeTreatedAsNullable(expression.operand.type))
            return Visit(CreateNullableGetValueCall(expression.syntax, expression.operand, expression.type));

        return base.VisitNullAssertOperator(expression);
    }

    internal static BoundExpression CreateNullableGetValueCall(
        SyntaxNode syntax,
        BoundExpression operand,
        TypeSymbol genericType) {
        return InstanceCall(
            syntax,
            operand,
            CorLibrary.GetWellKnownMember(WellKnownMembers.Nullable_getValue)
                .Construct([new TypeOrConstant(genericType)])
        );
    }

    internal override BoundNode VisitCastExpression(BoundCastExpression expression) {
        /*

        (<type>)<operand>

        ----> <operand> is nullable and <type> is nullable

        (HasValue(<operand>) ? new Nullable( (<type!>)Value(<operand>) ) : null)

        ----> <operand> is nullable and <type> is not nullable

        (<type>)Value(<operand>)

        ----> <operand> is not nullable and <type> is nullable

        new Nullable( (<type!>)<operand> )

        ----> <operand>.type == <type>

        <operand>

        */
        var syntax = expression.syntax;
        var operand = expression.operand;
        var type = expression.type;
        var operandType = operand.type;

        if (operandType?.Equals(type, TypeCompareKind.ConsiderEverything) ?? false)
            return Visit(operand);

        if (expression.conversion.underlyingConversions == default)
            return base.VisitCastExpression(expression);

        if (operandType.IsNullableType() && type.IsNullableType()) {
            return VisitConditionalOperator(
                Conditional(syntax,
                    @if: HasValue(syntax, operand),
                    @then: CreateNullable(syntax,
                        Cast(syntax,
                            type.GetNullableUnderlyingType(),
                            Value(syntax, operand, operandType.GetNullableUnderlyingType()),
                            expression.conversion.underlyingConversions[0],
                            operand.constantValue
                        ),
                        type
                    ),
                    @else: Literal(syntax, null, type),
                    type
                )
            );
        }

        switch (expression.conversion.kind) {
            case ConversionKind.ImplicitNullable:
                return Visit(
                    CreateNullable(
                        syntax,
                        Cast(
                            syntax,
                            type.GetNullableUnderlyingType(),
                            operand,
                            expression.conversion.underlyingConversions[0],
                            operand.constantValue
                        ),
                        type
                    )
                );
            case ConversionKind.ExplicitNullable:
                return Visit(
                    Cast(
                        syntax,
                        type,
                        Value(syntax, operand, operandType.GetNullableUnderlyingType()),
                        expression.conversion.underlyingConversions[0],
                        operand.constantValue
                    )
                );
        }

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
}
