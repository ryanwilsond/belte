using System;
using System.Collections.Generic;
using System.Linq;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.CodeGeneration;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;
using Buckle.Libraries;
using Buckle.Utilities;
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

        var rewrittenStatement = Optimizer.Optimize(statement);

        rewrittenStatement = FlowLowerer.Lower(rewrittenStatement, diagnostics);
        rewrittenStatement = lowerer._expander.Expand(rewrittenStatement);
        rewrittenStatement = (BoundStatement)lowerer.Visit(rewrittenStatement);
        rewrittenStatement = Flatten(method, (BoundBlockStatement)rewrittenStatement);
        rewrittenStatement = Optimizer.Optimize(rewrittenStatement);

        return (BoundBlockStatement)rewrittenStatement;
    }

    internal override BoundNode Visit(BoundNode node) {
        if (node is null)
            return null;

        if (node is BoundExpression e && e.constantValue is not null)
            return VisitConstant(e);

        return base.Visit(node);
    }

    internal override BoundNode VisitAssignmentOperator(BoundAssignmentOperator expression) {
        /*

        <left> = <right>

        ----> <left> is nullable and <right> is not nullable

        <left> = new Nullable(<right>)

        */
        if (expression.left.Type().IsNullableType() &&
            !expression.right.Type().IsNullableType() &&
            CodeGenerator.IsValueType(expression.right.Type())) {
            var syntax = expression.syntax;

            return VisitAssignmentOperator(
                Assignment(
                    syntax,
                    expression.left,
                    CreateNullable(syntax, expression.right, expression.left.Type()),
                    expression.isRef,
                    expression.Type()
                )
            );
        }

        return base.VisitAssignmentOperator(expression);
    }

    internal override BoundNode VisitFieldAccessExpression(BoundFieldAccessExpression node) {
        /*

        <receiver>.<field>

        ----> <field> is fixed

        &(<receiver>.<field>)

        */
        var syntax = node.syntax;
        var result = (BoundFieldAccessExpression)base.VisitFieldAccessExpression(node);

        if (node.field.isFixedSizeBuffer)
            return Visit(new BoundAddressOfOperator(syntax, result, true, node.type));

        return result;
    }

    internal override BoundNode VisitAddressOfOperator(BoundAddressOfOperator node) {
        if (node.isLoweredFixedField)
            return node;

        return base.VisitAddressOfOperator(node);
    }

    internal override BoundNode VisitStackAllocExpression(BoundStackAllocExpression node) {
        /*

        stackalloc <type>[<count>]

        ----> <count> is 0

        nullptr

        */
        var syntax = node.syntax;
        var type = node.type;

        if ((int)node.count.constantValue.value == 0)
            return new BoundLiteralExpression(node.syntax, new ConstantValue(null, SpecialType.None), type);

        var elementType = node.elementType;
        var rewrittenCount = (BoundExpression)Visit(node.count);

        if (type.typeKind == TypeKind.Pointer) {
            var stackSize = RewriteStackAllocCountToSize(syntax, rewrittenCount, elementType);
            return new BoundConvertedStackAllocExpression(syntax, elementType, stackSize, type);
        } else {
            throw ExceptionUtilities.UnexpectedValue(type);
        }
    }

    private BoundExpression RewriteStackAllocCountToSize(
        SyntaxNode syntax,
        BoundExpression countExpression,
        TypeSymbol elementType) {
        var uint32 = CorLibrary.GetSpecialType(SpecialType.UInt32);
        var int32 = CorLibrary.GetSpecialType(SpecialType.Int32);
        var uintptr = CorLibrary.GetSpecialType(SpecialType.UIntPtr);

        var sizeInBytes = elementType.specialType.SizeInBytes();
        var sizeOfConstant = sizeInBytes > 0 ? new ConstantValue(sizeInBytes, SpecialType.Int32) : null;

        var sizeOf = new BoundSizeOfOperator(syntax,
            new BoundTypeExpression(syntax, new TypeWithAnnotations(elementType), null, elementType),
            sizeOfConstant,
            int32
        );

        var sizeConst = sizeOf.constantValue;

        if (sizeConst is not null) {
            var size = (int)sizeConst.value;

            var countConst = countExpression.constantValue;

            if (countConst is not null) {
                var count = (int)countConst.value;
                var folded = unchecked((uint)count * size);

                if (folded < uint.MaxValue) {
                    return new BoundCastExpression(syntax,
                        Literal(syntax, (uint)folded, uint32),
                        Conversion.ExplicitIntegerToPointer,
                        null,
                        uintptr
                    );
                }
            }
        }

        var convertedCount = new BoundCastExpression(syntax,
            countExpression,
            Conversion.ExplicitNumeric,
            null,
            uint32
        );

        convertedCount = new BoundCastExpression(syntax,
            convertedCount,
            Conversion.ExplicitIntegerToPointer,
            null,
            uintptr
        );

        if ((int?)sizeConst?.value == 1)
            return convertedCount;

        return Binary(syntax,
            convertedCount,
            BinaryOperatorKind.UIntMultiplication,
            sizeOf,
            uintptr
        );
    }

    internal override BoundNode VisitLocalDeclarationStatement(BoundLocalDeclarationStatement statement) {
        /*

        <type> <localSymbol> = <initializer>

        ----> <localSymbol> is nullable and <initializer> is not nullable

        <type> <localSymbol> = new Nullable(<initializer>);

        */
        var declaration = statement.declaration;

        if (declaration.dataContainer.type.IsNullableType() &&
            !declaration.initializer.Type().IsNullableType() &&
            CodeGenerator.IsValueType(declaration.initializer.Type())) {
            var syntax = statement.syntax;
            return VisitLocalDeclarationStatement(new BoundLocalDeclarationStatement(syntax,
                new BoundDataContainerDeclaration(syntax,
                    declaration.dataContainer,
                    CreateNullable(
                        syntax,
                        declaration.initializer,
                        CorLibrary.GetOrCreateNullableType(declaration.initializer.Type())
                    )
                )
            ));
        }

        return base.VisitLocalDeclarationStatement(statement);
    }

    internal override BoundNode VisitConditionalOperator(BoundConditionalOperator expression) {
        /*

        <condition> ? <trueExpr> : <falseExpr>

        ----> <condition> is nullable

        goto <label> if <condition>.get_Value()

        */
        var condition = (BoundExpression)Visit(expression.condition);

        if (condition.constantValue is null && condition.Type().IsNullableType()) {
            var syntax = expression.syntax;

            return VisitConditionalOperator(
                new BoundConditionalOperator(
                    syntax,
                    RewriteNull(syntax, condition),
                    expression.isRef,
                    expression.trueExpression,
                    expression.falseExpression,
                    null,
                    expression.Type()
                )
            );
        }

        return base.VisitConditionalOperator(expression);
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
            condition.Type().IsNullableType()) {
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
    }

    internal override BoundNode VisitIndexerAccessExpression(BoundIndexerAccessExpression node) {
        /*

        <receiver>[<index>]

        ----> node has a method attached

        <method>(<receiver>, <index>)

        */
        var syntax = node.syntax;

        if (node.method is not null)
            return Visit(Call(syntax, node.method, node.receiver, node.index));

        return base.VisitIndexerAccessExpression(node);
    }

    internal override BoundNode VisitPointerIndexAccessExpression(BoundPointerIndexAccessExpression node) {
        /*

        <operand>[<index>]

        ---->

        ( *((<type>*)((<nuint>)<operand> + (<nuint>)<index> * (<nuint>)sizeof(<type>))) )

        ----> <index> is 0

        ( *<operand> )

        ! *technically* sizeof(UIntPtr) does not definitionally equal C/C++ size_t, but it is accurate for nearly all architectures

        */
        var syntax = node.syntax;
        var ptrType = (PointerTypeSymbol)node.receiver.Type();
        var resultType = node.type;

        if (node.index.constantValue is not null && Convert.ToInt32(node.index.constantValue.value) == 0) {
            return Visit(
                new BoundPointerIndirectionOperator(syntax,
                    node.receiver,
                    node.refersToLocation,
                    resultType
                )
            );
        }

        var int32 = CorLibrary.GetSpecialType(SpecialType.Int32);
        var sizeInBytes = resultType.specialType.SizeInBytes();
        var constantValue = sizeInBytes > 0 ? new ConstantValue(sizeInBytes, SpecialType.Int32) : null;

        var binaryType = UIntPtr.Size switch {
            4 => CorLibrary.GetSpecialType(SpecialType.UInt32),
            8 => CorLibrary.GetSpecialType(SpecialType.UInt64),
            _ => throw ExceptionUtilities.UnexpectedValue(UIntPtr.Size)
        };

        return Visit(
            new BoundPointerIndirectionOperator(syntax,
                Cast(syntax,
                    ptrType,
                    Binary(syntax,
                        Cast(syntax,
                            binaryType,
                            node.receiver,
                            Conversion.ExplicitPointerToInteger,
                            null
                        ),
                        BinaryOperatorKind.UIntAddition,
                        Binary(syntax,
                            Cast(syntax,
                                binaryType,
                                node.index,
                                Conversion.ImplicitNumeric,
                                null
                            ),
                            BinaryOperatorKind.UIntMultiplication,
                            Cast(syntax,
                                binaryType,
                                new BoundSizeOfOperator(syntax,
                                    new BoundTypeExpression(syntax,
                                        new TypeWithAnnotations(resultType),
                                        null,
                                        resultType
                                    ),
                                    constantValue,
                                    int32
                                ),
                                Conversion.ImplicitNumeric,
                                null
                            ),
                            binaryType
                        ),
                        binaryType
                    ),
                    Conversion.ExplicitIntegerToPointer,
                    null
                ),
                node.refersToLocation,
                resultType
            )
        );
    }

    internal override BoundNode VisitBinaryOperator(BoundBinaryOperator expression) {
        /*

        <left> <op> <right>

        ----> <op> has a method attached

        <method>(<left>, <right>)

        ----> <op> is && or ||

        ((<left> ?? false) <op> (<right> ?? false))

        ----> <left> is nullable and <right> is nullable

        ((HasValue(<left>) && HasValue(<right>)) ? new Nullable( Value(<left>) <op> Value(<right>) ) : null)

        ----> <left> is nullable

        (HasValue(<left>) ? new Nullable( Value(<left>) <op> <right> ) : null)

        ----> <right> is nullable

        (<right> isnt null ? new Nullable( <left> <op> Value(<right>) ) : null)

        */
        var syntax = expression.syntax;
        var op = expression.operatorKind;
        var type = expression.Type();

        if (op.Operator() == BinaryOperatorKind.Power) {
            return Visit(
                Call(
                    syntax,
                    StandardLibrary.GetPowerMethod(op.IsLifted(), op.OperandTypes() == BinaryOperatorKind.Int),
                    expression.left,
                    expression.right
                )
            );
        }

        if (expression.method is not null)
            return Visit(Call(syntax, expression.method, expression.left, expression.right));

        if (op.IsLifted() || op.IsConditional()) {
            var left = expression.left;
            var right = expression.right;

            if (left is BoundCastExpression lCast &&
                lCast.conversion.kind == ConversionKind.ImplicitNullable &&
                lCast.conversion.underlyingConversions[0].kind == ConversionKind.Identity) {
                left = lCast.operand;
            }

            if (right is BoundCastExpression rCast &&
                rCast.conversion.kind == ConversionKind.ImplicitNullable &&
                rCast.conversion.underlyingConversions[0].kind == ConversionKind.Identity) {
                right = rCast.operand;
            }

            var leftIsNullable = left.Type().IsNullableType();
            var rightIsNullable = right.Type().IsNullableType();

            if (op.IsConditional() && (leftIsNullable || rightIsNullable)) {
                var coalescedLeft = leftIsNullable
                    ? new BoundNullCoalescingOperator(syntax, left, Literal(syntax, false, type), false, null, type)
                    : left;

                var coalescedRight = rightIsNullable
                    ? new BoundNullCoalescingOperator(syntax, right, Literal(syntax, false, type), false, null, type)
                    : right;

                return VisitBinaryOperator(Binary(syntax, coalescedLeft, op, coalescedRight, type));
            }

            if (leftIsNullable &&
                rightIsNullable &&
                left.constantValue is null &&
                right.constantValue is null) {
                return VisitConditionalOperator(
                    Conditional(syntax,
                        @if: And(syntax,
                            HasValue(syntax, left),
                            HasValue(syntax, right)
                        ),
                        @then: CreateNullable(syntax,
                            Binary(syntax,
                                Value(syntax, left, left.Type().GetNullableUnderlyingType()),
                                op,
                                Value(syntax, right, right.Type().GetNullableUnderlyingType()),
                                type.StrippedType()
                                ),
                            type
                        ),
                        @else: Literal(syntax, null, type),
                        type
                    )
                );
            }

            if (leftIsNullable && left.constantValue is null) {
                return VisitConditionalOperator(
                    Conditional(syntax,
                        @if: HasValue(syntax, left),
                        @then: CreateNullable(syntax,
                            Binary(syntax,
                                Value(syntax, left, left.Type().GetNullableUnderlyingType()),
                                op,
                                DeNull(right),
                                type.StrippedType()
                            ),
                            type
                        ),
                        @else: Literal(syntax, null, type),
                        type
                    )
                );
            }

            if (rightIsNullable && right.constantValue is null) {
                return VisitConditionalOperator(
                    Conditional(syntax,
                        @if: HasValue(syntax, right),
                        @then: CreateNullable(syntax,
                            Binary(syntax,
                                DeNull(left),
                                op,
                                Value(syntax, right, right.Type().GetNullableUnderlyingType()),
                                type.StrippedType()
                            ),
                            type
                        ),
                        @else: Literal(syntax, null, type),
                        type
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

        ----> isPropagation

        (HasValue(<left>) ? <right> : <left>)

        */
        var syntax = expression.syntax;

        if (expression.isPropagation) {
            return VisitConditionalOperator(
                Conditional(syntax,
                    @if: HasValue(syntax, expression.left),
                    @then: expression.right,
                    @else: expression.left,
                    expression.Type()
                )
            );
        } else {
            return VisitConditionalOperator(
                Conditional(syntax,
                    @if: HasValue(syntax, expression.left),
                    @then: Value(syntax, expression.left, expression.left.StrippedType()),
                    @else: expression.right,
                    expression.Type()
                )
            );
        }
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

        if (op.IsLifted() && expression.operand.Type().IsNullableType()) {
            return VisitConditionalOperator(
                Conditional(syntax,
                    @if: HasValue(syntax, expression.operand),
                    @then: CreateNullable(syntax,
                        Unary(syntax,
                            op,
                            Value(syntax, expression.operand, expression.operand.Type().GetNullableUnderlyingType()),
                            expression.StrippedType()
                        ),
                        expression.type
                    ),
                    @else: Literal(syntax, null, expression.Type()),
                    expression.Type()
                )
            );
        }

        return base.VisitUnaryOperator(expression);
    }

    internal override BoundNode VisitArrayAccessExpression(BoundArrayAccessExpression expression) {
        var syntax = expression.syntax;

        if (expression.index.Type().IsNullableType()) {
            return Visit(new BoundArrayAccessExpression(syntax,
                expression.receiver,
                RewriteNull(syntax, expression.index),
                expression.constantValue,
                expression.Type()
            ));
        }

        return base.VisitArrayAccessExpression(expression);
    }

    internal override BoundNode VisitInitializerList(BoundInitializerList expression) {
        /*

        <list>

        ---->

        new Array() <list>

        */
        var syntax = expression.syntax;
        var sizeType = CorLibrary.GetSpecialType(SpecialType.Int);

        return VisitArrayCreationExpression(
            new BoundArrayCreationExpression(
                syntax,
                [Literal(syntax, (long)expression.items.Length, sizeType)],
                VisitNonIsolatedList(expression),
                expression.Type()
            )
        );
    }

    private BoundInitializerList VisitNonIsolatedList(BoundInitializerList expression) {
        var syntax = expression.syntax;
        var arrayType = (ArrayTypeSymbol)expression.StrippedType();
        ArrayBuilder<BoundExpression>? newList = null;

        for (var i = 0; i < expression.items.Length; i++) {
            var item = expression.items[i];
            var visited = VisitListItem(item);

            if (newList is null && item != visited) {
                newList = ArrayBuilder<BoundExpression>.GetInstance();

                if (i > 0)
                    newList.AddRange(expression.items, i);
            }

            if (newList is not null && visited is not null)
                newList.Add((BoundExpression)visited);
        }

        if (newList is not null)
            return new BoundInitializerList(syntax, newList.ToImmutableAndFree(), expression.Type());

        return expression;

        BoundNode VisitListItem(BoundExpression item) {
            if (ShouldBeTreatedAsNullable(arrayType.elementType) &&
                !item.Type().IsNullableType()) {
                if (item.constantValue is null)
                    return Visit(CreateNullable(syntax, item, arrayType.elementType));
                else
                    return VisitConstant(Literal(syntax, item.constantValue.value, arrayType.elementType));
            }

            return Visit(item);
        }
    }

    internal override BoundNode VisitArrayCreationExpression(BoundArrayCreationExpression expression) {
        var sizes = VisitList(expression.sizes);

        var initializer = expression.initializer is null
            ? null
            : VisitNonIsolatedList(expression.initializer);

        var type = VisitType(expression.Type());
        return expression.Update(sizes, initializer, type);
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
            if (ShouldBeTreatedAsNullable(expression.left.Type())) {
                var call = InstanceCall(
                    syntax,
                    expression.left,
                    CreateNullableGetHasValueSymbol(expression.left.Type().GetNullableUnderlyingType())
                );

                if (expression.isNot)
                    return Visit(call);

                return Visit(Unary(syntax, UnaryOperatorKind.BoolLogicalNegation, call, call.Type()));
            }

            var left = (BoundExpression)Visit(expression.left);

            return new BoundIsOperator(
                syntax,
                left,
                expression.right,
                expression.isNot,
                expression.constantValue,
                expression.type
            );
        }

        return base.VisitIsOperator(expression);
    }

    internal override BoundNode VisitNullAssertOperator(BoundNullAssertOperator expression) {
        /*

        <operand>!

        ---->

        <operand>.get_Value

        */
        if (ShouldBeTreatedAsNullable(expression.operand.Type()))
            return Visit(CreateNullableGetValueCall(expression.syntax, expression.operand, expression.Type()));

        return base.VisitNullAssertOperator(expression);
    }

    internal static BoundExpression CreateNullableGetValueCall(
        SyntaxNode syntax,
        BoundExpression operand,
        TypeSymbol genericType) {
        return InstanceCall(
            syntax,
            operand,
            CreateNullableGetValueSymbol(genericType)
        );
    }

    private static MethodSymbol CreateNullableGetValueSymbol(TypeSymbol genericType) {
        return CreateMethodAsMemberOfNullable(
            CorLibrary.GetWellKnownMember(WellKnownMembers.Nullable_getValue),
            genericType
        );
    }

    private static MethodSymbol CreateNullableGetHasValueSymbol(TypeSymbol genericType) {
        return CreateMethodAsMemberOfNullable(
            CorLibrary.GetWellKnownMember(WellKnownMembers.Nullable_getHasValue),
            genericType
        );
    }

    private static MethodSymbol CreateNullableCtorSymbol(TypeSymbol genericType) {
        return CreateMethodAsMemberOfNullable(
            CorLibrary.GetWellKnownMember(WellKnownMembers.Nullable_ctor),
            genericType
        );
    }

    private static MethodSymbol CreateMethodAsMemberOfNullable(MethodSymbol method, TypeSymbol genericType) {
        return (MethodSymbol)method.SymbolAsMember(
            CorLibrary.GetSpecialType(SpecialType.Nullable).Construct([new TypeOrConstant(genericType)])
        );
    }

    internal override BoundNode VisitCastExpression(BoundCastExpression expression) {
        /*

        (<type>)<operand>

        ----> <op> has a method attached

        <method>(<operand>)

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

        if (expression.conversion.method is not null)
            return Visit(Call(syntax, expression.conversion.method, expression.operand));

        var operand = expression.operand;
        var type = expression.Type();
        var operandType = operand.Type();

        if (operandType?.Equals(type, TypeCompareKind.ConsiderEverything) ?? false)
            return Visit(operand);

        if (expression.conversion.underlyingConversions == default) {
            if (expression.conversion.kind is ConversionKind.ImplicitNullToPointer)
                return expression;

            return base.VisitCastExpression(expression);
        }

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

        if (method.name == "Value" && !expression.arguments[0].Type().IsNullableType())
            return Visit(expression.arguments[0]);
        else if (method.name == "HasValue" && !expression.arguments[0].Type().IsNullableType())
            return Literal(syntax, true, expression.Type());

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
                expression.Type()
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
                new BoundBinaryOperator(
                    syntax,
                    expression.left,
                    expression.right,
                    expression.op.kind,
                    expression.op.method,
                    ConstantFolding.FoldBinary(
                        expression.left,
                        expression.right,
                        expression.op.kind,
                        expression.Type(),
                        syntax.location,
                        BelteDiagnosticQueue.Discarded
                    ),
                    expression.Type()
                ),
                false,
                expression.Type()
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
                    expression.isPropagation,
                    null,
                    expression.Type()
                ),
                false,
                expression.Type()
            )
        );
    }

    internal static BoundBlockStatement Flatten(MethodSymbol method, BoundBlockStatement statement) {
        return FlattenBlock(method, statement, true);
    }

    private static BoundBlockStatement FlattenBlock(MethodSymbol method, BoundBlockStatement block, bool needsReturn) {
        var syntax = block.syntax;
        var statementsBuilder = ArrayBuilder<BoundStatement>.GetInstance();
        var localsBuilder = ArrayBuilder<DataContainerSymbol>.GetInstance();
        var functionsBuilder = ArrayBuilder<LocalFunctionSymbol>.GetInstance();

        var stack = new Stack<BoundStatement>();
        stack.Push(block);

        while (stack.Count > 0) {
            var current = stack.Pop();

            if (current is BoundBlockStatement blockStatement) {
                localsBuilder.AddRange(blockStatement.locals);
                functionsBuilder.AddRange(blockStatement.localFunctions);

                foreach (var s in blockStatement.statements.Reverse())
                    stack.Push(s);
            } else if (current is BoundTryStatement tryStatement) {
                var hasCatch = tryStatement.catchBody is not null;
                var hasFinally = tryStatement.finallyBody is not null;

                statementsBuilder.Add(tryStatement.Update(
                    FlattenBlock(method, (BoundBlockStatement)tryStatement.body, false),
                    hasCatch ? FlattenBlock(method, (BoundBlockStatement)tryStatement.catchBody, false) : null,
                    hasFinally ? FlattenBlock(method, (BoundBlockStatement)tryStatement.finallyBody, false) : null
                ));
            } else {
                statementsBuilder.Add(current);
            }
        }

        if (method.returnsVoid && needsReturn) {
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

    private static bool ShouldBeTreatedAsNullable(TypeSymbol type) {
        return type.IsNullableType() && CodeGenerator.IsValueType(type.GetNullableUnderlyingType());
    }

    private BoundExpression CreateNullable(
        SyntaxNode syntax,
        BoundExpression expression,
        TypeSymbol nullableType) {
        if (!ShouldBeTreatedAsNullable(nullableType))
            return expression;

        if (expression is BoundObjectCreationExpression creation &&
            creation.type.specialType == SpecialType.Nullable) {
            return expression;
        }

        return new BoundObjectCreationExpression(
            syntax,
            CreateNullableCtorSymbol(nullableType.GetNullableUnderlyingType()),
            [expression],
            default,
            default,
            default,
            false,
            nullableType
        );
    }

    internal static BoundNode VisitConstant(BoundExpression expression) {
        var syntax = expression.syntax;
        var type = expression.Type();

        if (expression.constantValue.value is null)
            type = CorLibrary.GetOrCreateNullableType(type);

        return new BoundLiteralExpression(
            syntax,
            expression.constantValue,
            ShouldBeTreatedAsNullable(type) ? type : type.StrippedType()
        );
    }

    private static BoundExpression RewriteNull(SyntaxNode syntax, BoundExpression expression) {
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
                binary.StrippedType()
            );
        }

        if (expression.Type().IsNullableType())
            return Value(syntax, expression, expression.Type().GetNullableUnderlyingType());

        return expression;
    }

    private BoundExpression DeNull(BoundExpression expression) {
        if (expression.constantValue is null)
            return expression;

        return new BoundLiteralExpression(expression.syntax, expression.constantValue, expression.StrippedType());
    }
}
