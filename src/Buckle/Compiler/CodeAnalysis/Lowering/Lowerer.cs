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
/// This lowerer directly can only lower simple expressions that do not reference a child more than one.
/// In a case where a child must be used more than once, the Expander should handle that node instead to create a temp.
/// Nodes may be visited multiple times.
/// </summary>
internal sealed class Lowerer : BoundTreeRewriter {
    private readonly Expander _expander;

    private bool _sawCompileTimeExpression;

    private Lowerer(MethodSymbol container, BelteDiagnosticQueue diagnostics) {
        _expander = new Expander(container, diagnostics);
    }

    internal static BoundBlockStatement Lower(
        OptimizationLevel optimizationLevel,
        MethodSymbol method,
        BoundStatement statement,
        BelteDiagnosticQueue diagnostics,
        out bool sawCompileTimeExpression) {
        var lowerer = new Lowerer(method, diagnostics);
        var optimize = optimizationLevel == OptimizationLevel.Release;

        var rewrittenStatement = statement;

        if (optimize)
            rewrittenStatement = Optimizer.Optimize(rewrittenStatement);

        rewrittenStatement = FlowLowerer.Lower(method, rewrittenStatement, diagnostics);
        rewrittenStatement = lowerer._expander.Expand(rewrittenStatement);
        rewrittenStatement = (BoundStatement)lowerer.Visit(rewrittenStatement);
        rewrittenStatement = Flatten(method, (BoundBlockStatement)rewrittenStatement);

        if (optimize)
            rewrittenStatement = Optimizer.Optimize(rewrittenStatement);

        sawCompileTimeExpression = lowerer._sawCompileTimeExpression;

        return (BoundBlockStatement)rewrittenStatement;
    }

    internal override BoundNode Visit(BoundNode node) {
        if (node is null)
            return null;

        if (node is BoundExpression e && e.constantValue is not null)
            return VisitConstant(e);

        return base.Visit(node);
    }

    internal override BoundNode VisitCompileTimeExpression(BoundCompileTimeExpression node) {
        _sawCompileTimeExpression = true;
        return base.VisitCompileTimeExpression(node);
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

        ----> <field> is of anonymous union

        <receiver>.<Union>.<field>

        */
        var syntax = node.syntax;
        var field = node.field;

        if (field.isAnonymousUnionMember) {
            var containingType = (SourceNamedTypeSymbol)field.containingType;
            var union = containingType.anonymousUnionTypes[field];
            var unionField = containingType.anonymousUnionFields[union];
            var receiver = (BoundExpression)Visit(node.receiver);

            return new BoundFieldAccessExpression(syntax,
                new BoundFieldAccessExpression(syntax,
                    receiver,
                    unionField,
                    null,
                    union
                ),
                field,
                null,
                node.type
            );
        }

        var result = (BoundFieldAccessExpression)base.VisitFieldAccessExpression(node);

        if (field.isFixedSizeBuffer)
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
                expression.Update(
                    RewriteNull(syntax, condition),
                    expression.isRef,
                    expression.trueExpression,
                    expression.falseExpression,
                    expression.constantValue,
                    expression.type
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

    internal override BoundNode VisitArrayAccessExpression(BoundArrayAccessExpression expression) {
        var syntax = expression.syntax;

        if (expression.index.Type().IsNullableType()) {
            return Visit(expression.Update(
                expression.receiver,
                RewriteNull(syntax, expression.index),
                expression.constantValue,
                expression.type
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

    internal override BoundNode VisitIsOperator(BoundIsOperator expression) {
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
        if (ShouldBeTreatedAsNullable(expression.operand.Type())) {
            if (expression.throwIfNull)
                return Visit(CreateNullableGetValueCall(expression.syntax, expression.operand, expression.Type()));
            else
                return Visit(CreateNullableGetValueOrDefaultCall(expression.syntax, expression.operand, expression.Type()));
        }

        return base.VisitNullAssertOperator(expression);
    }

    internal override BoundNode VisitDefaultExpression(BoundDefaultExpression node) {
        /*

        default

        ----> <type> is pointer

        nullptr

        */
        var syntax = node.syntax;
        var type = node.type;

        if (type.IsPointerOrFunctionPointer() || type.specialType is SpecialType.IntPtr or SpecialType.UIntPtr)
            return Visit(Cast(syntax, type, Literal(syntax, null, type), Conversion.ImplicitNullToPointer, null));

        return base.VisitDefaultExpression(node);
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

    internal static BoundExpression CreateNullableGetValueOrDefaultCall(
        SyntaxNode syntax,
        BoundExpression operand,
        TypeSymbol genericType) {
        return InstanceCall(
            syntax,
            operand,
            CreateNullableGetValueOrDefaultSymbol(genericType)
        );
    }

    private static MethodSymbol CreateNullableGetValueOrDefaultSymbol(TypeSymbol genericType) {
        return CreateMethodAsMemberOfNullable(
            CorLibrary.GetWellKnownMember(WellKnownMembers.Nullable_GetValueOrDefault),
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

    internal override BoundNode VisitCastExpression(BoundCastExpression node) {
        if (node.conversion.kind == ConversionKind.ImplicitNullToPointer)
            return node;

        return base.VisitCastExpression(node);
    }

    internal override BoundNode VisitCallExpression(BoundCallExpression expression) {
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
            expression.Update(
                expression.receiver,
                expression.method,
                arguments,
                expression.argumentRefKinds,
                expression.defaultArguments,
                expression.resultKind,
                expression.type
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

    internal static bool CanFallThrough(BoundStatement boundStatement) {
        return boundStatement.kind != BoundKind.ReturnStatement &&
            boundStatement.kind != BoundKind.GotoStatement;
    }

    internal static bool ShouldBeTreatedAsNullable(TypeSymbol type) {
        return type.IsNullableType() && CodeGenerator.IsValueType(type.GetNullableUnderlyingType());
    }

    internal static BoundExpression CreateNullable(
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

    internal static BoundExpression VisitConstant(BoundExpression expression) {
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

    internal static BoundExpression RewriteNull(SyntaxNode syntax, BoundExpression expression) {
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

    internal static BoundExpression DeNull(BoundExpression expression) {
        if (expression.constantValue is null)
            return expression;

        return new BoundLiteralExpression(expression.syntax, expression.constantValue, expression.StrippedType());
    }
}
