using System.Collections.Generic;
using System.Linq;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.CodeGeneration;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;
using Buckle.Libraries;
using Buckle.Utilities;
using static Buckle.CodeAnalysis.Binding.BoundFactory;

namespace Buckle.CodeAnalysis.Lowering;

/// <summary>
/// For lowering expressions into statements. Any lowering that can't produce statements should be done in a later pass.
/// Most commonly to produce temporary locals to prevent side-effect duplication.
/// Nodes can be revisited.
/// </summary>
internal sealed class Expander : BoundTreeExpander {
    private readonly BelteDiagnosticQueue _diagnostics;

    internal Expander(MethodSymbol container, BelteDiagnosticQueue diagnostics) {
        _container = container;
        _diagnostics = diagnostics;
    }

    private protected override MethodSymbol _container { get; set; }

    internal BoundStatement Expand(BoundStatement statement) {
        return Simplify(statement.syntax, ExpandStatement(statement));
    }

    private protected override List<BoundStatement> ExpandExpression(
        BoundExpression expression,
        out BoundExpression replacement,
        UseKind useKind = UseKind.Value) {
        if (expression.constantValue is not null) {
            replacement = Lowerer.VisitConstant(expression);
            return [];
        }

        return base.ExpandExpression(expression, out replacement, useKind);
    }

    private protected override List<BoundStatement> ExpandCascadeListExpression(
        BoundCascadeListExpression expression,
        out BoundExpression replacement,
        UseKind useKind) {
        var syntax = expression.syntax;
        var statements = ExpandExpression(expression.receiver, out var newReceiver, UseKind.Writable);
        var tempLocal = GenerateTempLocal(expression.Type());

        statements.Add(
            new BoundLocalDeclarationStatement(syntax, new BoundDataContainerDeclaration(
                syntax,
                tempLocal,
                newReceiver
            ))
        );

        for (var i = 0; i < expression.cascades.Length; i++) {
            var cascade = expression.cascades[i];
            var isConditional = expression.conditionals[i];

            switch (cascade.kind) {
                case BoundKind.ConditionalAccessExpression: {
                        var condAccess = (BoundConditionalAccessExpression)cascade;

                        statements.AddRange(
                            ExpandStatement(new BoundExpressionStatement(syntax,
                                condAccess.Update(
                                    Local(syntax, tempLocal),
                                    condAccess.accessExpression,
                                    condAccess.type
                                )
                            ))
                        );
                    }

                    break;
                case BoundKind.CallExpression: {
                        var call = (BoundCallExpression)cascade;
                        var replacementReceiver = Local(syntax, tempLocal);
                        statements.AddRange(ExpandExpressionList(call.arguments, out var arguments));

                        statements.Add(
                            new BoundExpressionStatement(syntax,
                                call.Update(
                                    replacementReceiver,
                                    call.method,
                                    arguments,
                                    call.argumentRefKinds,
                                    call.defaultArguments,
                                    call.resultKind,
                                    call.type
                                )
                            )
                        );
                    }

                    break;
                case BoundKind.CompoundAssignmentOperator: {
                        var assignment = (BoundCompoundAssignmentOperator)cascade;
                        var leftAccess = isConditional
                            ? (BoundFieldAccessExpression)(assignment.left as BoundConditionalAccessExpression)
                                .accessExpression
                            : (BoundFieldAccessExpression)assignment.left;

                        statements.AddRange(
                            ExpandStatement(new BoundExpressionStatement(syntax,
                                assignment.Update(
                                    MakeReplacementReceiver(syntax, isConditional, tempLocal, leftAccess),
                                    assignment.right,
                                    assignment.op,
                                    assignment.leftPlaceholder,
                                    assignment.leftConversion,
                                    assignment.finalPlaceholder,
                                    assignment.finalConversion,
                                    assignment.resultKind,
                                    assignment.originalUserDefinedOperators,
                                    assignment.type
                                )
                            ))
                        );
                    }

                    break;
                case BoundKind.NullCoalescingAssignmentOperator: {
                        var assignment = (BoundNullCoalescingAssignmentOperator)cascade;
                        var leftAccess = isConditional
                            ? (BoundFieldAccessExpression)(assignment.left as BoundConditionalAccessExpression)
                                .accessExpression
                            : (BoundFieldAccessExpression)assignment.left;

                        statements.AddRange(
                            ExpandStatement(new BoundExpressionStatement(syntax,
                                assignment.Update(
                                    MakeReplacementReceiver(syntax, isConditional, tempLocal, leftAccess),
                                    assignment.right,
                                    assignment.isPropagation,
                                    assignment.type
                                )
                            ))
                        );
                    }

                    break;
                case BoundKind.AssignmentOperator: {
                        var assignment = (BoundAssignmentOperator)cascade;
                        var leftAccess = isConditional
                            ? (BoundFieldAccessExpression)(assignment.left as BoundConditionalAccessExpression)
                                .accessExpression
                            : (BoundFieldAccessExpression)assignment.left;

                        statements.AddRange(
                            ExpandStatement(new BoundExpressionStatement(syntax,
                                assignment.Update(
                                    MakeReplacementReceiver(syntax, isConditional, tempLocal, leftAccess),
                                    assignment.right,
                                    assignment.isRef,
                                    assignment.type
                                )
                            ))
                        );
                    }

                    break;
                default:
                    throw ExceptionUtilities.Unreachable();
            }
        }

        replacement = Local(syntax, tempLocal);
        return statements;

        static BoundExpression MakeReplacementReceiver(
            SyntaxNode syntax,
            bool isConditional,
            SynthesizedDataContainerSymbol tempLocal,
            BoundFieldAccessExpression leftAccess) {
            return isConditional
                ? new BoundConditionalAccessExpression(
                    syntax,
                    Local(syntax, tempLocal),
                    leftAccess,
                    leftAccess.type)
                : new BoundFieldAccessExpression(
                    syntax,
                    Local(syntax, tempLocal),
                    leftAccess.field,
                    leftAccess.constantValue,
                    leftAccess.type
                );
        }
    }

    private protected override List<BoundStatement> ExpandLocalFunctionStatement(
        BoundLocalFunctionStatement statement) {
        var oldContainer = _container;
        _container = statement.symbol;
        var newStatements = base.ExpandLocalFunctionStatement(statement);
        _container = oldContainer;
        return newStatements;
    }

    private protected override List<BoundStatement> ExpandLocalDeclarationStatement(
        BoundLocalDeclarationStatement statement) {
        _localNames.Add(statement.declaration.dataContainer.name);
        return base.ExpandLocalDeclarationStatement(statement);
    }

    private protected override List<BoundStatement> ExpandFieldAccessExpression(
        BoundFieldAccessExpression expression,
        out BoundExpression replacement,
        UseKind useKind) {
        /*

        <receiver>.<field>

        ----> UseKind.Value, UseKind.Writable

        <receiver>.<field>

        ----> UseKind.StableValue

        temp = <receiver>.<field>
        temp

        */
        var statements = base.ExpandFieldAccessExpression(expression, out replacement, UseKind.Value);

        if (expression.type.IsNullableType() && expression.type.GetNullableUnderlyingType().IsStructType()) {
            // TODO Just need to make sure this is actually unreachable then can remove
            throw ExceptionUtilities.Unreachable();
            // var syntax = expression.syntax;
            // var underlyingType = type.GetNullableUnderlyingType();

            // var statements = ExpandExpression(expression.receiver, out var newReceiver, useKind);

            // newReceiver = Lowerer.CreateNullableGetValueCall(syntax, newReceiver, underlyingType);
            // var tempLocal = GenerateTempLocal(type);

            // statements.AddRange(
            //     new BoundLocalDeclarationStatement(syntax,
            //         new BoundDataContainerDeclaration(syntax, tempLocal, newReceiver)
            //     )
            // );

            // replacement = new BoundFieldAccessExpression(syntax,
            //     Local(syntax, tempLocal),
            //     expression.field,
            //     expression.constantValue,
            //     expression.field.type
            // );

            // _accessDepth = savedAccessDepth;
            // return statements;
        }

        if (useKind == UseKind.StableValue)
            return Stabilize(expression.syntax, statements, replacement, out replacement);

        return statements;
    }

    private protected override List<BoundStatement> ExpandCompoundAssignmentOperator(
        BoundCompoundAssignmentOperator expression,
        out BoundExpression replacement,
        UseKind useKind) {
        /*

        <left> <op>= <right>

        ----> UseKind.Value

        <left> = <left> <op> <right>

        ---> UseKind.StableValue, UseKind.Writable

        <left> = <left> <op> <right>
        <left>

        */
        var syntax = expression.syntax;

        var statements = ExpandExpression(expression.left, out var newLeft, UseKind.Writable);
        statements.AddRange(ExpandExpression(expression.right, out var newRight));

        statements.AddRange(ExpandAssignmentOperator(Assignment(syntax,
            newLeft,
            new BoundBinaryOperator(syntax,
                newLeft,
                newRight,
                expression.op.kind,
                expression.op.method,
                ConstantFolding.FoldBinary(
                    newLeft,
                    newRight,
                    expression.op.kind,
                    expression.Type(),
                    syntax.location,
                    BelteDiagnosticQueue.Discarded
                ),
                expression.type
            ),
            false,
            expression.type
        ), out var assignment, UseKind.Value));

        if (useKind == UseKind.Value) {
            replacement = assignment;
            return statements;
        } else {
            statements.Add(new BoundExpressionStatement(syntax, assignment));
            replacement = newLeft;
            return statements;
        }
    }

    private protected override List<BoundStatement> ExpandNullCoalescingOperator(
        BoundNullCoalescingOperator expression,
        out BoundExpression replacement,
        UseKind useKind) {
        /*

        <left> <op> <right>

        ----> <op> is ??

        temp = <left>
        goto Break unless temp is null
        temp = <right>
        Break:
        temp

        ---> <op> is ?!

        temp = <left>
        goto Break if temp is null
        temp = <right>
        Break:
        temp

        */
        var syntax = expression.syntax;

        var statements = ExpandExpression(expression.left, out var newLeft);
        var temp = GenerateTempLocal(newLeft.type);
        statements.Add(LocalDeclaration(syntax, temp, newLeft));

        var condition = IsNull(syntax, Local(syntax, temp));
        var breakLabel = GenerateLabel();
        statements.Add(
            expression.isPropagation
                ? GotoIf(syntax, breakLabel, condition)
                : GotoIfNot(syntax, breakLabel, condition)
        );

        statements.AddRange(ExpandExpression(expression.right, out var newRight));
        var assignment = Assignment(syntax, Local(syntax, temp), newRight, false, expression.type);
        statements.Add(new BoundExpressionStatement(syntax, assignment));
        statements.Add(Label(syntax, breakLabel));

        replacement = Local(syntax, temp);
        return statements;
    }

    private protected override List<BoundStatement> ExpandNullErasureOperator(
        BoundNullErasureOperator expression,
        out BoundExpression replacement,
        UseKind useKind) {
        /*

        <operand>?

        ---->

        temp = <operand>
        goto Break unless temp is null
        temp = <default value>
        Break:
        temp!

        */
        var syntax = expression.syntax;

        var statements = ExpandExpression(expression.operand, out var newOperand);
        var temp = GenerateTempLocal(newOperand.type);
        statements.Add(LocalDeclaration(syntax, temp, newOperand));

        var condition = IsNull(syntax, Local(syntax, temp));
        var breakLabel = GenerateLabel();
        statements.Add(GotoIfNot(syntax, breakLabel, condition));

        var defaultValue = Literal(syntax, expression.defaultValue.value, expression.type);
        var assignment = Assignment(syntax, Local(syntax, temp), defaultValue, false, expression.type);
        statements.Add(new BoundExpressionStatement(syntax, assignment));
        statements.Add(Label(syntax, breakLabel));

        replacement = new BoundNullAssertOperator(syntax, Local(syntax, temp), false, null, expression.type);
        return statements;
    }

    private protected override List<BoundStatement> ExpandNullCoalescingAssignmentOperator(
        BoundNullCoalescingAssignmentOperator expression,
        out BoundExpression replacement,
        UseKind useKind) {
        /*

        <left> <op>= <right>

        ----> <op> is ??

        goto Break unless <left> is null
        <left> = <right>
        Break:
        <left>

        ---> <op> is ?!

        goto Break if <left> is null
        <left> = <right>
        Break:
        <left>

        */
        var syntax = expression.syntax;

        var statements = ExpandExpression(expression.left, out var newLeft, UseKind.Writable);

        var condition = IsNull(syntax, newLeft);
        var breakLabel = GenerateLabel();
        statements.Add(
            expression.isPropagation
                ? GotoIf(syntax, breakLabel, condition)
                : GotoIfNot(syntax, breakLabel, condition)
        );

        statements.AddRange(ExpandExpression(expression.right, out var newRight));
        var assignment = Assignment(syntax, newLeft, newRight, false, expression.type);
        statements.Add(new BoundExpressionStatement(syntax, assignment));
        statements.Add(Label(syntax, breakLabel));

        replacement = newLeft;
        return statements;
    }

    private protected override List<BoundStatement> ExpandCallExpression(
        BoundCallExpression expression,
        out BoundExpression replacement,
        UseKind useKind) {
        var statements = base.ExpandCallExpression(expression, out replacement, UseKind.Value);

        if (useKind == UseKind.Writable && expression.method.returnsByRef)
            return statements;
        else
            return StabilizeIfNecessary(expression.syntax, useKind, statements, replacement, out replacement);
    }

    private protected override List<BoundStatement> ExpandBinaryOperator(
        BoundBinaryOperator expression,
        out BoundExpression replacement,
        UseKind useKind) {
        /*

        (Multiple options can happen in junction)

        <left> <op> <right>

        ----> UseKind.StableValue

        temp = <left> <op> <right>
        temp

        ----> <op> has a method attached

        <method>(<left>, <right>)

        ----> <op> is **

        Math.Pow(<left>, <right>)

        ----> <left> is nullable and <right> is nullable

        ((HasValue(<left>) && HasValue(<right>)) ? new Nullable( Value(<left>) <op> Value(<right>) ) : null)

        ----> <left> is nullable

        (HasValue(<left>) ? new Nullable( Value(<left>) <op> <right> ) : null)

        ----> <right> is nullable

        (<right> isnt null ? new Nullable( <left> <op> Value(<right>) ) : null)

        */
        var op = expression.operatorKind;

        if (op.IsConditional()) {
            if (op.Operator() == BinaryOperatorKind.And)
                return ExpandConditionalAndOperator(expression, out replacement);
            else if (op.Operator() == BinaryOperatorKind.Or)
                return ExpandConditionalOrOperator(expression, out replacement);
            else
                throw ExceptionUtilities.UnexpectedValue(op);
        }

        var syntax = expression.syntax;

        if (expression.method is not null) {
            var statements = ExpandExpression(expression.left, out var newLeft);
            statements.AddRange(ExpandExpression(expression.right, out var newRight));
            replacement = Call(
                syntax,
                expression.method,
                newLeft,
                newRight
            );

            return StabilizeIfNecessary(syntax, useKind, statements, replacement, out replacement);
        }

        if (op.Operator() == BinaryOperatorKind.Power) {
            var statements = ExpandExpression(expression.left, out var newLeft);
            statements.AddRange(ExpandExpression(expression.right, out var newRight));
            replacement = Call(
                syntax,
                StandardLibrary.GetPowerMethod(op.IsLifted(), op.OperandTypes() == BinaryOperatorKind.Int),
                newLeft,
                newRight
            );

            return StabilizeIfNecessary(syntax, useKind, statements, replacement, out replacement);
        }

        var type = expression.Type();
        var left = expression.left;
        var right = expression.right;

        if (op.IsLifted()) {
            // Optimization: We don't need to check if something is nullable if it was lifted just for this operator
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
        }

        var leftIsNullable = left.Type().IsNullableType();
        var rightIsNullable = right.Type().IsNullableType();

        if (leftIsNullable && rightIsNullable && left.constantValue is null && right.constantValue is null) {
            var statements = ExpandExpression(left, out var newLeft, UseKind.StableValue);
            statements.AddRange(ExpandExpression(right, out var newRight, UseKind.StableValue));
            replacement = Conditional(syntax,
                @if: And(syntax,
                    HasValue(syntax, newLeft),
                    HasValue(syntax, newRight)
                ),
                @then: Lowerer.CreateNullable(syntax,
                    Binary(syntax,
                        Value(syntax, newLeft, newLeft.Type().StrippedType()),
                        op,
                        Value(syntax, newRight, newRight.Type().StrippedType()),
                        type.StrippedType()
                        ),
                    type
                ),
                @else: Literal(syntax, null, type),
                type
            );
            return StabilizeIfNecessary(syntax, useKind, statements, replacement, out replacement);
        } else if (leftIsNullable && left.constantValue is null) {
            var statements = ExpandExpression(left, out var newLeft, UseKind.StableValue);
            statements.AddRange(ExpandExpression(right, out var newRight));
            replacement = Conditional(syntax,
                @if: HasValue(syntax, newLeft),
                @then: Lowerer.CreateNullable(syntax,
                    Binary(syntax,
                        Value(syntax, newLeft, newLeft.Type().StrippedType()),
                        op,
                        Lowerer.DeNull(newRight),
                        type.StrippedType()
                    ),
                    type
                ),
                @else: Literal(syntax, null, type),
                type
            );
            return StabilizeIfNecessary(syntax, useKind, statements, replacement, out replacement);
        } else if (rightIsNullable && right.constantValue is null) {
            var statements = ExpandExpression(left, out var newLeft);
            statements.AddRange(ExpandExpression(right, out var newRight, UseKind.StableValue));
            replacement = Conditional(syntax,
                @if: HasValue(syntax, newRight),
                @then: Lowerer.CreateNullable(syntax,
                    Binary(syntax,
                        Lowerer.DeNull(newLeft),
                        op,
                        Value(syntax, newRight, newRight.Type().StrippedType()),
                        type.StrippedType()
                    ),
                    type
                ),
                @else: Literal(syntax, null, type),
                type
            );
            return StabilizeIfNecessary(syntax, useKind, statements, replacement, out replacement);
        } else {
            var statements = base.ExpandBinaryOperator(expression, out replacement, UseKind.Value);
            return StabilizeIfNecessary(syntax, useKind, statements, replacement, out replacement);
        }
    }

    private List<BoundStatement> ExpandConditionalAndOperator(
        BoundBinaryOperator expression,
        out BoundExpression replacement) {
        /*

        <left> && <right>

        ----> <left> is nullable and <right> is nullable

        result = false
        goto break if <left> is null || <left>! == false
        goto break if <right> is null
        result = <right>!
        break:
        result

        ----> <left> is nullable

        result = false
        goto break if <left> is null || <left>! == false
        result = <right>
        break:
        result

        ----> <right> is nullable

        result = false
        goto break if <left> == false
        goto break if <right> is null
        result = <right>!
        break:
        result

        ---->

        result = <left>
        goto break if result == false
        result = <right>
        break:
        result

        */
        var syntax = expression.syntax;
        var boolType = CorLibrary.GetSpecialType(SpecialType.Bool);

        // TODO There is probably potential for short cutting if left and right are "simple" (e.g. `a && b`)

        if (expression.left.Type().IsNullableType() && expression.right.Type().IsNullableType()) {
            var statements = ExpandExpression(expression.left, out var newLeft, UseKind.StableValue);
            var temp = GenerateTempLocal(boolType);
            var breakLabel = GenerateLabel();
            statements.Add(LocalDeclaration(syntax, temp, Literal(syntax, false, boolType)));
            statements.Add(GotoIf(syntax, breakLabel,
                Binary(syntax,
                    IsNull(syntax, newLeft),
                    BinaryOperatorKind.BoolConditionalOr,
                    Binary(syntax,
                        new BoundNullAssertOperator(syntax, newLeft, false, null, newLeft.StrippedType()),
                        BinaryOperatorKind.BoolEqual,
                        Literal(syntax, false, boolType),
                        boolType
                    ),
                    boolType
                )
            ));
            statements.AddRange(ExpandExpression(expression.right, out var newRight, UseKind.StableValue));
            statements.Add(GotoIf(syntax, breakLabel, IsNull(syntax, newRight)));
            statements.Add(new BoundExpressionStatement(syntax,
                Assignment(syntax,
                    Local(syntax, temp),
                    new BoundNullAssertOperator(syntax, newRight, false, null, newRight.StrippedType()),
                    false,
                    temp.type
                )
            ));
            statements.Add(Label(syntax, breakLabel));

            replacement = Local(syntax, temp);
            return statements;
        } else if (expression.left.Type().IsNullableType()) {
            var statements = ExpandExpression(expression.left, out var newLeft, UseKind.StableValue);
            var temp = GenerateTempLocal(boolType);
            var breakLabel = GenerateLabel();
            statements.Add(LocalDeclaration(syntax, temp, Literal(syntax, false, boolType)));
            statements.Add(GotoIf(syntax, breakLabel,
                Binary(syntax,
                    IsNull(syntax, newLeft),
                    BinaryOperatorKind.BoolConditionalOr,
                    Binary(syntax,
                        new BoundNullAssertOperator(syntax, newLeft, false, null, newLeft.StrippedType()),
                        BinaryOperatorKind.BoolEqual,
                        Literal(syntax, false, boolType),
                        boolType
                    ),
                    boolType
                )
            ));
            statements.AddRange(ExpandExpression(expression.right, out var newRight));
            statements.Add(GotoIf(syntax, breakLabel, IsNull(syntax, newRight)));
            statements.Add(new BoundExpressionStatement(syntax,
                Assignment(syntax,
                    Local(syntax, temp),
                    newRight,
                    false,
                    temp.type
                )
            ));
            statements.Add(Label(syntax, breakLabel));

            replacement = Local(syntax, temp);
            return statements;
        } else if (expression.right.Type().IsNullableType()) {
            var statements = ExpandExpression(expression.left, out var newLeft);
            var temp = GenerateTempLocal(boolType);
            var breakLabel = GenerateLabel();
            statements.Add(LocalDeclaration(syntax, temp, Literal(syntax, false, boolType)));
            statements.Add(GotoIf(syntax, breakLabel,
                Binary(syntax,
                    newLeft,
                    BinaryOperatorKind.BoolEqual,
                    Literal(syntax, false, boolType),
                    boolType
                )
            ));
            statements.AddRange(ExpandExpression(expression.right, out var newRight, UseKind.StableValue));
            statements.Add(GotoIf(syntax, breakLabel, IsNull(syntax, newRight)));
            statements.Add(new BoundExpressionStatement(syntax,
                Assignment(syntax,
                    Local(syntax, temp),
                    new BoundNullAssertOperator(syntax, newRight, false, null, newRight.StrippedType()),
                    false,
                    temp.type
                )
            ));
            statements.Add(Label(syntax, breakLabel));

            replacement = Local(syntax, temp);
            return statements;
        } else {
            var statements = ExpandExpression(expression.left, out var newLeft);
            var temp = GenerateTempLocal(boolType);
            var breakLabel = GenerateLabel();
            statements.Add(LocalDeclaration(syntax, temp, newLeft));
            statements.Add(GotoIf(syntax, breakLabel,
                Binary(syntax,
                    newLeft,
                    BinaryOperatorKind.BoolEqual,
                    Literal(syntax, false, boolType),
                    boolType
                )
            ));
            statements.AddRange(ExpandExpression(expression.right, out var newRight));
            statements.Add(new BoundExpressionStatement(syntax,
                Assignment(syntax,
                    Local(syntax, temp),
                    newRight,
                    false,
                    temp.type
                )
            ));
            statements.Add(Label(syntax, breakLabel));

            replacement = Local(syntax, temp);
            return statements;
        }
    }

    private List<BoundStatement> ExpandConditionalOrOperator(
        BoundBinaryOperator expression,
        out BoundExpression replacement) {
        /*

        <left> || <right>

        ----> <left> is nullable and <right> is nullable

        result = true
        goto break unless <left> is null || <left>! == false
        goto continue unless <right> is null
        result = false
        goto break
        continue:
        result = <right>!
        break:
        result

        ----> <left> is nullable

        result = true
        goto break unless <left> is null || <left>! == false
        result = <right>
        break:
        result

        ----> <right> is nullable

        result = true
        goto break if <left> == true
        goto continue unless <right> is null
        result = false
        goto break
        continue:
        result = <right>!
        break:
        result

        ---->

        result = <left>
        goto break if result == true
        result = <right>
        break:
        result

        */
        var syntax = expression.syntax;
        var boolType = CorLibrary.GetSpecialType(SpecialType.Bool);

        if (expression.left.Type().IsNullableType() && expression.right.Type().IsNullableType()) {
            var statements = ExpandExpression(expression.left, out var newLeft, UseKind.StableValue);
            var temp = GenerateTempLocal(boolType);
            var breakLabel = GenerateLabel();
            var continueLabel = GenerateLabel();
            statements.Add(LocalDeclaration(syntax, temp, Literal(syntax, true, boolType)));
            statements.Add(GotoIfNot(syntax, breakLabel,
                Binary(syntax,
                    IsNull(syntax, newLeft),
                    BinaryOperatorKind.BoolConditionalOr,
                    Binary(syntax,
                        new BoundNullAssertOperator(syntax, newLeft, false, null, newLeft.StrippedType()),
                        BinaryOperatorKind.BoolEqual,
                        Literal(syntax, false, boolType),
                        boolType
                    ),
                    boolType
                )
            ));
            statements.AddRange(ExpandExpression(expression.right, out var newRight, UseKind.StableValue));
            statements.Add(GotoIfNot(syntax, continueLabel, IsNull(syntax, newRight)));
            statements.Add(new BoundExpressionStatement(syntax,
                Assignment(syntax,
                    Local(syntax, temp),
                    Literal(syntax, false, temp.type),
                    false,
                    temp.type
                )
            ));
            statements.Add(Goto(syntax, breakLabel));
            statements.Add(Label(syntax, continueLabel));
            statements.Add(new BoundExpressionStatement(syntax,
                Assignment(syntax,
                    Local(syntax, temp),
                    new BoundNullAssertOperator(syntax, newRight, false, null, newRight.StrippedType()),
                    false,
                    temp.type
                )
            ));
            statements.Add(Label(syntax, breakLabel));

            replacement = Local(syntax, temp);
            return statements;
        } else if (expression.left.Type().IsNullableType()) {
            var statements = ExpandExpression(expression.left, out var newLeft, UseKind.StableValue);
            var temp = GenerateTempLocal(boolType);
            var breakLabel = GenerateLabel();
            statements.Add(LocalDeclaration(syntax, temp, Literal(syntax, true, boolType)));
            statements.Add(GotoIfNot(syntax, breakLabel,
                Binary(syntax,
                    IsNull(syntax, newLeft),
                    BinaryOperatorKind.BoolConditionalOr,
                    Binary(syntax,
                        new BoundNullAssertOperator(syntax, newLeft, false, null, newLeft.StrippedType()),
                        BinaryOperatorKind.BoolEqual,
                        Literal(syntax, false, boolType),
                        boolType
                    ),
                    boolType
                )
            ));
            statements.AddRange(ExpandExpression(expression.right, out var newRight));
            statements.Add(new BoundExpressionStatement(syntax,
                Assignment(syntax,
                    Local(syntax, temp),
                    newRight,
                    false,
                    temp.type
                )
            ));
            statements.Add(Label(syntax, breakLabel));

            replacement = Local(syntax, temp);
            return statements;
        } else if (expression.right.Type().IsNullableType()) {
            var statements = ExpandExpression(expression.left, out var newLeft);
            var temp = GenerateTempLocal(boolType);
            var breakLabel = GenerateLabel();
            var continueLabel = GenerateLabel();
            statements.Add(LocalDeclaration(syntax, temp, Literal(syntax, true, boolType)));
            statements.Add(GotoIf(syntax, breakLabel,
                Binary(syntax,
                    newLeft,
                    BinaryOperatorKind.BoolEqual,
                    Literal(syntax, true, boolType),
                    boolType
                )
            ));
            statements.AddRange(ExpandExpression(expression.right, out var newRight, UseKind.StableValue));
            statements.Add(GotoIfNot(syntax, continueLabel, IsNull(syntax, newRight)));
            statements.Add(new BoundExpressionStatement(syntax,
                Assignment(syntax,
                    Local(syntax, temp),
                    Literal(syntax, false, temp.type),
                    false,
                    temp.type
                )
            ));
            statements.Add(Goto(syntax, breakLabel));
            statements.Add(Label(syntax, continueLabel));
            statements.Add(new BoundExpressionStatement(syntax,
                Assignment(syntax,
                    Local(syntax, temp),
                    new BoundNullAssertOperator(syntax, newRight, false, null, newRight.StrippedType()),
                    false,
                    temp.type
                )
            ));
            statements.Add(Label(syntax, breakLabel));

            replacement = Local(syntax, temp);
            return statements;
        } else {
            var statements = ExpandExpression(expression.left, out var newLeft);
            var temp = GenerateTempLocal(boolType);
            var breakLabel = GenerateLabel();
            statements.Add(LocalDeclaration(syntax, temp, newLeft));
            statements.Add(GotoIf(syntax, breakLabel,
                Binary(syntax,
                    newLeft,
                    BinaryOperatorKind.BoolEqual,
                    Literal(syntax, true, boolType),
                    boolType
                )
            ));
            statements.AddRange(ExpandExpression(expression.right, out var newRight));
            statements.Add(new BoundExpressionStatement(syntax,
                Assignment(syntax,
                    Local(syntax, temp),
                    newRight,
                    false,
                    temp.type
                )
            ));
            statements.Add(Label(syntax, breakLabel));

            replacement = Local(syntax, temp);
            return statements;
        }
    }

    private List<BoundStatement> StabilizeIfNecessary(
        SyntaxNode syntax,
        UseKind useKind,
        List<BoundStatement> statements,
        BoundExpression tentativeReplacement,
        out BoundExpression replacement) {
        if (useKind == UseKind.Value) {
            replacement = tentativeReplacement;
            return statements;
        } else if (useKind == UseKind.StableValue) {
            return Stabilize(syntax, statements, tentativeReplacement, out replacement);
        } else {
            throw ExceptionUtilities.UnexpectedValue(useKind);
        }
    }

    private List<BoundStatement> Stabilize(
        SyntaxNode syntax,
        List<BoundStatement> statements,
        BoundExpression expression,
        out BoundExpression replacement) {
        var temp = GenerateTempLocal(expression.type);
        statements.Add(LocalDeclaration(syntax, temp, expression));
        replacement = Local(syntax, temp);
        return statements;
    }

    private protected override List<BoundStatement> ExpandUnaryOperator(
        BoundUnaryOperator expression,
        out BoundExpression replacement,
        UseKind useKind) {
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
        var operand = expression.operand;

        if (expression.method is not null) {
            var statements = ExpandExpression(operand, out var newOperand);
            replacement = Call(
                syntax,
                expression.method,
                newOperand
            );

            return StabilizeIfNecessary(syntax, useKind, statements, replacement, out replacement);
        }

        var op = expression.operatorKind;

        if (op == UnaryOperatorKind.UnaryPlus)
            return ExpandExpression(operand, out replacement, useKind);

        if (operand.Type().IsNullableType()) {
            var statements = ExpandExpression(operand, out var newOperand);
            replacement = Conditional(syntax,
                @if: HasValue(syntax, newOperand),
                @then: Lowerer.CreateNullable(syntax,
                    Unary(syntax,
                        op,
                        Value(syntax, newOperand, newOperand.Type().GetNullableUnderlyingType()),
                        expression.StrippedType()
                    ),
                    expression.type
                ),
                @else: Literal(syntax, null, expression.Type()),
                expression.Type()
            );
            return StabilizeIfNecessary(syntax, useKind, statements, replacement, out replacement);
        } else {
            var statements = base.ExpandUnaryOperator(expression, out replacement, UseKind.Value);
            return StabilizeIfNecessary(syntax, useKind, statements, replacement, out replacement);
        }
    }

    private protected override List<BoundStatement> ExpandCastExpression(
        BoundCastExpression expression,
        out BoundExpression replacement,
        UseKind useKind) {
        /*

        (<type>)<operand>

        ----> <type> is a FunctionType

        Func(&<operand>)

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
        var operand = expression.operand;

        if (expression.conversion.kind == ConversionKind.MethodGroup) {
            replacement = new BoundFunctionLoad(syntax, expression.conversion.method, expression.type);
            return [];
        }

        if (expression.conversion.method is not null) {
            var statements = ExpandExpression(operand, out var newOperand);
            replacement = Call(
                syntax,
                expression.conversion.method,
                newOperand
            );

            return StabilizeIfNecessary(syntax, useKind, statements, replacement, out replacement);
        }

        var type = expression.Type();
        var operandType = operand.Type();

        if (operandType?.Equals(type, TypeCompareKind.ConsiderEverything) ?? false)
            return ExpandExpression(operand, out replacement, useKind);

        if (expression.conversion.underlyingConversions == default) {
            if (expression.conversion.kind is ConversionKind.ImplicitNullToPointer)
                return StabilizeIfNecessary(syntax, useKind, [], expression, out replacement);

            var statements = base.ExpandCastExpression(expression, out replacement, UseKind.Value);
            return StabilizeIfNecessary(syntax, useKind, statements, replacement, out replacement);
        }

        if (operandType.IsNullableType() && type.IsNullableType()) {
            var statements = ExpandExpression(operand, out var newOperand, UseKind.StableValue);
            statements.AddRange(ExpandExpression(Conditional(syntax,
                @if: HasValue(syntax, newOperand),
                @then: Lowerer.CreateNullable(syntax,
                    Cast(syntax,
                        type.GetNullableUnderlyingType(),
                        Value(syntax, newOperand, operandType.GetNullableUnderlyingType()),
                        expression.conversion.underlyingConversions[0],
                        newOperand.constantValue
                    ),
                    type
                ),
                @else: Literal(syntax, null, type),
                type
            ), out replacement, UseKind.Value));
            return StabilizeIfNecessary(syntax, useKind, statements, replacement, out replacement);
        }

        {
            List<BoundStatement> statements;

            switch (expression.conversion.kind) {
                case ConversionKind.ImplicitNullable:
                    statements = ExpandExpression(Lowerer.CreateNullable(
                        syntax,
                        Cast(
                            syntax,
                            type.GetNullableUnderlyingType(),
                            operand,
                            expression.conversion.underlyingConversions[0],
                            operand.constantValue
                        ),
                        type
                    ), out replacement, UseKind.Value);
                    break;
                case ConversionKind.ExplicitNullable:
                    statements = ExpandExpression(Cast(
                        syntax,
                        type,
                        Value(syntax, operand, operandType.GetNullableUnderlyingType()),
                        expression.conversion.underlyingConversions[0],
                        operand.constantValue
                    ), out replacement, UseKind.Value);
                    break;
                default:
                    statements = ExpandExpression(operand, out replacement, UseKind.Value);
                    break;
            }

            return StabilizeIfNecessary(syntax, useKind, statements, replacement, out replacement);
        }
    }

    private protected override List<BoundStatement> ExpandIncrementOperator(
        BoundIncrementOperator expression,
        out BoundExpression replacement,
        UseKind useKind) {
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
        var operand = expression.operand;

        if (expression.method is not null) {
            var statements = ExpandExpression(operand, out var newOperand);
            replacement = Call(
                syntax,
                expression.method,
                newOperand
            );

            return StabilizeIfNecessary(syntax, useKind, statements, replacement, out replacement);
        }

        var op = expression.operatorKind.Operator();
        var isIsolated = syntax.parent.kind == SyntaxKind.ExpressionStatement;

        if (op == UnaryOperatorKind.PrefixIncrement || (op == UnaryOperatorKind.PostfixIncrement && isIsolated))
            return ExpandCompoundAssignmentOperator(Increment(syntax, operand), out replacement, useKind);

        if (op == UnaryOperatorKind.PrefixDecrement || (op == UnaryOperatorKind.PostfixDecrement && isIsolated))
            return ExpandCompoundAssignmentOperator(Decrement(syntax, operand), out replacement, useKind);

        if (op == UnaryOperatorKind.PostfixIncrement) {
            var statements = ExpandExpression(operand, out var newOperand, UseKind.Writable);
            var temp = GenerateTempLocal(newOperand.type);
            statements.Add(LocalDeclaration(syntax, temp, newOperand));
            statements.AddRange(ExpandCompoundAssignmentOperator(Increment(syntax, newOperand), out var expr, useKind));
            statements.Add(new BoundExpressionStatement(syntax, expr));
            replacement = Local(syntax, temp);
            return statements;
        } else if (op == UnaryOperatorKind.PostfixDecrement) {
            var statements = ExpandExpression(operand, out var newOperand, UseKind.Writable);
            var temp = GenerateTempLocal(newOperand.type);
            statements.Add(LocalDeclaration(syntax, temp, newOperand));
            statements.AddRange(ExpandCompoundAssignmentOperator(Decrement(syntax, newOperand), out var expr, useKind));
            statements.Add(new BoundExpressionStatement(syntax, expr));
            replacement = Local(syntax, temp);
            return statements;
        } else {
            throw ExceptionUtilities.UnexpectedValue(op);
        }
    }

    private protected override List<BoundStatement> ExpandConditionalOperator(
        BoundConditionalOperator expression,
        out BoundExpression replacement,
        UseKind useKind) {
        var statements = base.ExpandConditionalOperator(expression, out replacement, UseKind.Value);
        return StabilizeIfNecessary(expression.syntax, useKind, statements, replacement, out replacement);
    }

    private protected override List<BoundStatement> ExpandInitializerList(
        BoundInitializerList expression,
        out BoundExpression replacement,
        UseKind useKind) {
        var statements = base.ExpandInitializerList(expression, out replacement, UseKind.Value);
        return StabilizeIfNecessary(expression.syntax, useKind, statements, replacement, out replacement);
    }

    private protected override List<BoundStatement> ExpandAsOperator(
        BoundAsOperator expression,
        out BoundExpression replacement,
        UseKind useKind) {
        var statements = base.ExpandAsOperator(expression, out replacement, UseKind.Value);
        return StabilizeIfNecessary(expression.syntax, useKind, statements, replacement, out replacement);
    }

    private protected override List<BoundStatement> ExpandIsOperator(
        BoundIsOperator expression,
        out BoundExpression replacement,
        UseKind useKind) {
        var statements = base.ExpandIsOperator(expression, out replacement, UseKind.Value);
        return StabilizeIfNecessary(expression.syntax, useKind, statements, replacement, out replacement);
    }

    private protected override List<BoundStatement> ExpandNullAssertOperator(
        BoundNullAssertOperator expression,
        out BoundExpression replacement,
        UseKind useKind) {
        var statements = base.ExpandNullAssertOperator(expression, out replacement, UseKind.Value);
        return StabilizeIfNecessary(expression.syntax, useKind, statements, replacement, out replacement);
    }

    private protected override List<BoundStatement> ExpandAddressOfOperator(
        BoundAddressOfOperator expression,
        out BoundExpression replacement,
        UseKind useKind) {
        var statements = base.ExpandAddressOfOperator(expression, out replacement, UseKind.Value);
        return StabilizeIfNecessary(expression.syntax, useKind, statements, replacement, out replacement);
    }

    private protected override List<BoundStatement> ExpandPointerIndirectionOperator(
        BoundPointerIndirectionOperator expression,
        out BoundExpression replacement,
        UseKind useKind) {
        var statements = base.ExpandPointerIndirectionOperator(expression, out replacement, UseKind.Value);

        if (useKind == UseKind.Writable)
            return statements;
        else
            return StabilizeIfNecessary(expression.syntax, useKind, statements, replacement, out replacement);
    }

    private protected override List<BoundStatement> ExpandArrayCreationExpression(
        BoundArrayCreationExpression expression,
        out BoundExpression replacement,
        UseKind useKind) {
        var statements = base.ExpandArrayCreationExpression(expression, out replacement, UseKind.Value);
        return StabilizeIfNecessary(expression.syntax, useKind, statements, replacement, out replacement);
    }

    private protected override List<BoundStatement> ExpandFunctionPointerLoad(
        BoundFunctionPointerLoad expression,
        out BoundExpression replacement,
        UseKind useKind) {
        var statements = base.ExpandFunctionPointerLoad(expression, out replacement, UseKind.Value);
        return StabilizeIfNecessary(expression.syntax, useKind, statements, replacement, out replacement);
    }

    private protected override List<BoundStatement> ExpandFunctionPointerCallExpression(
        BoundFunctionPointerCallExpression expression,
        out BoundExpression replacement,
        UseKind useKind) {
        var statements = base.ExpandFunctionPointerCallExpression(expression, out replacement, UseKind.Value);
        return StabilizeIfNecessary(expression.syntax, useKind, statements, replacement, out replacement);
    }

    private protected override List<BoundStatement> ExpandStackAllocExpression(
        BoundStackAllocExpression expression,
        out BoundExpression replacement,
        UseKind useKind) {
        var statements = base.ExpandStackAllocExpression(expression, out replacement, UseKind.Value);
        return StabilizeIfNecessary(expression.syntax, useKind, statements, replacement, out replacement);
    }

    private protected override List<BoundStatement> ExpandConvertedStackAllocExpression(
        BoundConvertedStackAllocExpression expression,
        out BoundExpression replacement,
        UseKind useKind) {
        var statements = base.ExpandConvertedStackAllocExpression(expression, out replacement, UseKind.Value);
        return StabilizeIfNecessary(expression.syntax, useKind, statements, replacement, out replacement);
    }

    private protected override List<BoundStatement> ExpandAssignmentOperator(
        BoundAssignmentOperator expression,
        out BoundExpression replacement,
        UseKind useKind) {
        /*

        <left> = <right>

        ----> <left> is conditional access <cond>

        temp = null
        goto break if <cond.receiver> is null
        temp = <cond.access> = <right>
        break:
        temp


        ----> <left> is conditional access <cond> and is isolated

        goto break if <cond.receiver> is null
        <cond.access> = <right>
        break:

        */
        if (expression.left is BoundConditionalAccessExpression condAccess) {
            var syntax = expression.syntax;
            List<BoundStatement> statements = [];
            var isIsolated = expression.syntax.parent.kind is SyntaxKind.ExpressionStatement or SyntaxKind.CascadeExpression;
            var breakLabel = GenerateLabel();
            var temp = GenerateTempLocal(expression.Type());

            if (!isIsolated)
                statements.Add(LocalDeclaration(syntax, temp, Literal(syntax, null, temp.type)));

            var linearChain = new List<BoundConditionalAccessExpression>();
            var current = condAccess;

            while (current is not null) {
                linearChain.Add(current);
                current = current.receiver as BoundConditionalAccessExpression;
            }

            ExpandExpression(linearChain.Last().receiver, out var newReceiver, UseKind.StableValue);
            statements.Add(GotoIf(syntax, breakLabel, IsNull(syntax, newReceiver)));

            for (var i = linearChain.Count - 1; i > 0; i--) {
                var cur = linearChain[i];
                var innerTemp = GenerateTempLocal(cur.Type());
                statements.AddRange(CreateConditionalAccess(cur, newReceiver, out var conditionalAccess));
                statements.Add(LocalDeclaration(syntax, innerTemp, conditionalAccess));
                newReceiver = Local(syntax, innerTemp);
                statements.Add(GotoIf(syntax, breakLabel, IsNull(syntax, newReceiver)));
            }

            statements.AddRange(ExpandExpression(expression.right, out var newRight));
            statements.AddRange(CreateConditionalAccess(linearChain[0], newReceiver, out var finalReceiver));
            var assignment = Assignment(
                syntax,
                finalReceiver,
                newRight,
                false,
                newReceiver.type
            );

            if (isIsolated) {
                statements.Add(new BoundExpressionStatement(syntax, assignment));
                statements.Add(Label(syntax, breakLabel));
                replacement = null;
            } else {
                statements.Add(new BoundExpressionStatement(syntax,
                    Assignment(syntax,
                        Local(syntax, temp),
                        assignment,
                        false,
                        assignment.type
                    )
                ));
                statements.Add(Label(syntax, breakLabel));
                replacement = Local(syntax, temp);
            }

            return statements;
        } else {
            var statements = base.ExpandAssignmentOperator(expression, out replacement, UseKind.Value);
            return StabilizeIfNecessary(expression.syntax, useKind, statements, replacement, out replacement);
        }

        List<BoundStatement> CreateConditionalAccess(
            BoundConditionalAccessExpression expression,
            BoundExpression currentReceiver,
            out BoundExpression newReceiver) {
            var access = expression.accessExpression;

            switch (access.kind) {
                case BoundKind.FieldAccessExpression:
                    var fieldAccess = (BoundFieldAccessExpression)access;
                    newReceiver = new BoundFieldAccessExpression(
                        access.syntax,
                        currentReceiver,
                        fieldAccess.field,
                        fieldAccess.constantValue,
                        fieldAccess.type
                    );
                    return [];
                case BoundKind.ArrayAccessExpression: {
                        var arrayAccess = (BoundArrayAccessExpression)access;
                        var statements = ExpandExpression(arrayAccess.index, out var newIndex);
                        newReceiver = new BoundArrayAccessExpression(
                            access.syntax,
                            currentReceiver,
                            newIndex,
                            arrayAccess.constantValue,
                            arrayAccess.type
                        );
                        return statements;
                    }
                case BoundKind.CallExpression: {
                        var call = (BoundCallExpression)access;
                        var statements = ExpandExpressionList(call.arguments, out var newArguments);
                        newReceiver = new BoundCallExpression(
                            access.syntax,
                            currentReceiver,
                            call.method,
                            newArguments,
                            call.argumentRefKinds,
                            call.defaultArguments,
                            call.resultKind,
                            call.type
                        );
                        return statements;
                    }
                default:
                    throw ExceptionUtilities.UnexpectedValue(access.kind);
            }
        }
    }

    private protected override List<BoundStatement> ExpandInitializerDictionary(
        BoundInitializerDictionary expression,
        out BoundExpression replacement,
        UseKind useKind) {
        var syntax = expression.syntax;
        var dictionaryType = (NamedTypeSymbol)expression.StrippedType();
        var tempLocal = GenerateTempLocal(expression.Type());
        var statements = new List<BoundStatement>() {
            new BoundLocalDeclarationStatement(syntax, new BoundDataContainerDeclaration(
                syntax,
                tempLocal,
                new BoundObjectCreationExpression(
                    syntax,
                    dictionaryType.instanceConstructors[0],
                    [],
                    [],
                    [],
                    default,
                    false,
                    expression.Type()
                )
            ))
        };

        var method = dictionaryType.GetMembers("Add").Single() as MethodSymbol;

        foreach (var pair in expression.items) {
            statements.AddRange(ExpandStatement(new BoundExpressionStatement(syntax, new BoundCallExpression(
                syntax,
                Local(syntax, tempLocal),
                method,
                [pair.Item1, pair.Item2],
                [RefKind.None, RefKind.None],
                default,
                LookupResultKind.Viable,
                method.returnType
            ))));
        }

        replacement = Local(syntax, tempLocal);
        return statements;
    }

    private protected override List<BoundStatement> ExpandConditionalAccessExpression(
        BoundConditionalAccessExpression expression,
        out BoundExpression replacement,
        UseKind useKind) {
        /*

        <receiver>?.<operand>

        ----> <operand> is a field

        <receiver> isnt null ? <receiver>.<field> : null

        ----> <operand> is an index, UseKind.Value

        <receiver> isnt null ? <receiver>[<index>] : null

        ----> <operand> is an index, UseKind.StableValue

        temp = <receiver> isnt null ? <receiver>[<index>] : null
        temp

        ----> <operand> is a method call, UseKind.Value

        <receiver> isnt null ? <receiver>.<call> : null

        ----> <operand> is a method call, UseKind.StableValue

        temp = <receiver> isnt null ? <receiver>.<call> : null
        temp

        */
        var syntax = expression.syntax;
        var access = expression.accessExpression;

        var statements = ExpandExpression(expression.receiver, out var newReceiver, UseKind.StableValue);

        BoundExpression trueExpression;

        if (access is BoundFieldAccessExpression f) {
            trueExpression = new BoundFieldAccessExpression(syntax, newReceiver, f.field, f.constantValue, f.Type());
        } else if (access is BoundArrayAccessExpression a) {
            statements.AddRange(ExpandExpression(a.index, out var indexReplacement));
            trueExpression = new BoundArrayAccessExpression(syntax, newReceiver, indexReplacement, null, a.Type());
        } else if (access is BoundCallExpression c) {
            statements.AddRange(ExpandExpressionList(c.arguments, out var replacementArguments));
            trueExpression = new BoundCallExpression(
                syntax,
                newReceiver,
                c.method,
                replacementArguments,
                c.argumentRefKinds,
                c.defaultArguments,
                c.resultKind,
                c.Type()
            );
        } else {
            throw ExceptionUtilities.Unreachable();
        }

        replacement = new BoundConditionalOperator(
            syntax,
            HasValue(syntax, newReceiver),
            false,
            trueExpression,
            Literal(syntax, null, access.Type()),
            null,
            access.Type()
        );

        if (access is BoundFieldAccessExpression)
            return statements;
        else
            return StabilizeIfNecessary(syntax, useKind, statements, replacement, out replacement);
    }

    private protected override List<BoundStatement> ExpandInterpolatedStringExpression(
        BoundInterpolatedStringExpression expression,
        out BoundExpression replacement,
        UseKind useKind) {
        var syntax = expression.syntax;
        var stringType = CorLibrary.GetSpecialType(SpecialType.String);
        var nullableStringType = CorLibrary.GetNullableType(SpecialType.String);
        var statements = new List<BoundStatement>();
        var tempLocal = GenerateTempLocal(stringType);
        replacement = Local(syntax, tempLocal);
        statements.Add(new BoundLocalDeclarationStatement(syntax, new BoundDataContainerDeclaration(syntax,
            tempLocal,
            Literal(syntax, string.Empty, stringType)
        )));

        // ? Null turns into empty strings instead of nulling the entire result

        foreach (var content in expression.contents) {
            BoundExpression right;

            if (content.constantValue?.specialType == SpecialType.String) {
                right = Literal(syntax, content.constantValue.value, stringType);
            } else {
                if (content.IsLiteralNull())
                    continue;

                statements.AddRange(ExpandExpression(content, out var replacementContent, UseKind.StableValue));

                if (replacementContent.StrippedType().specialType == SpecialType.String) {
                    right = replacementContent;
                } else if (replacementContent.Type().IsVerifierValue()) {
                    if (!replacementContent.Type().IsNullableType()) {
                        var conversion = Conversion.Classify(replacementContent.Type(), stringType);

                        if (!conversion.exists) {
                            _diagnostics.Push(
                                Error.CannotConvert(syntax.location, replacementContent.Type(), stringType)
                            );
                        }

                        right = Cast(syntax, stringType, replacementContent, conversion, null);
                    } else {
                        var conversion = Conversion.Classify(replacementContent.StrippedType(), stringType);

                        if (!conversion.exists) {
                            _diagnostics.Push(
                                Error.CannotConvert(syntax.location, replacementContent.StrippedType(), stringType)
                            );
                        }

                        right = new BoundConditionalOperator(syntax,
                            new BoundIsOperator(syntax,
                                replacementContent,
                                Literal(syntax, null, nullableStringType),
                                false,
                                null,
                                CorLibrary.GetSpecialType(SpecialType.Bool)
                            ),
                            false,
                            Literal(syntax, string.Empty, stringType),
                            Cast(syntax,
                                stringType,
                                new BoundNullAssertOperator(syntax,
                                    replacementContent,
                                    false,
                                    null,
                                    replacementContent.StrippedType()
                                ),
                                conversion,
                                null
                            ),
                            null,
                            stringType
                        );
                    }
                } else {
                    var toString = (MethodSymbol)CorLibrary.GetSpecialType(SpecialType.Object)
                        .GetMembers("ToString").Single(m => m is MethodSymbol);

                    var toStringTemp = GenerateTempLocal(nullableStringType);
                    right = Local(syntax, toStringTemp);

                    statements.Add(new BoundLocalDeclarationStatement(syntax, new BoundDataContainerDeclaration(syntax,
                        toStringTemp,
                        new BoundConditionalOperator(syntax,
                            new BoundIsOperator(syntax,
                                replacementContent,
                                Literal(syntax, null, nullableStringType),
                                false,
                                null,
                                CorLibrary.GetSpecialType(SpecialType.Bool)
                            ),
                            false,
                            Literal(syntax, null, nullableStringType),
                            new BoundCallExpression(syntax,
                                replacementContent,
                                toString,
                                [],
                                [],
                                BitVector.Empty,
                                LookupResultKind.Viable,
                                nullableStringType
                            ),
                            null,
                            nullableStringType
                        )
                    )));
                }
            }

            if (right.Type().IsNullableType()) {
                statements.AddRange(ExpandExpression(new BoundNullCoalescingOperator(syntax,
                    right,
                    Literal(syntax, string.Empty, stringType),
                    false,
                    null,
                    stringType
                ), out right, UseKind.Value));
            }

            statements.Add(new BoundExpressionStatement(syntax, Assignment(syntax,
                replacement,
                Binary(syntax,
                    replacement,
                    BinaryOperatorKind.StringConcatenation,
                    right,
                    stringType
                ),
                false,
                stringType
            )));
        }

        return statements;
    }
}
