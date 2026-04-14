using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Diagnostics;
using Buckle.Libraries;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// Resolves overloads to find the best overload.
/// </summary>
internal sealed partial class OverloadResolution {
    private const int BetterConversionTargetRecursionLimit = 100;

    private readonly Binder _binder;

    /// <summary>
    /// Creates an <see cref="OverloadResolution" />, uses a Binders diagnostics.
    /// </summary>
    /// <param name="binder"><see cref="Binder" /> to use diagnostics from.</param>
    internal OverloadResolution(Binder binder) {
        _binder = binder;
    }

    internal Conversions conversions => _binder.conversions;

    internal void FunctionPointerOverloadResolution(
        ArrayBuilder<FunctionPointerMethodSymbol> funcPtrBuilder,
        AnalyzedArguments analyzedArguments,
        OverloadResolutionResult<FunctionPointerMethodSymbol> overloadResolutionResult) {
        var typeArgumentsBuilder = ArrayBuilder<TypeOrConstant>.GetInstance();

        AddMemberToCandidateSet(
            funcPtrBuilder[0],
            overloadResolutionResult.resultsBuilder,
            funcPtrBuilder,
            typeArgumentsBuilder,
            analyzedArguments,
            completeResults: true,
            containingTypeMap: null
        );
    }

    internal void BinaryOperatorOverloadResolution(
        BinaryOperatorKind kind,
        BoundExpression left,
        BoundExpression right,
        BinaryOperatorOverloadResolutionResult result) {
        EasyOut(kind, left, right, result);

        if (result.results.Count > 0)
            return;

        NoEasyOut(kind, left, right, result);

        void EasyOut(
            BinaryOperatorKind kind,
            BoundExpression left,
            BoundExpression right,
            BinaryOperatorOverloadResolutionResult result) {
            var underlyingKind = kind & ~BinaryOperatorKind.Conditional;
            BinaryOperatorEasyOut(underlyingKind, left, right, result);
        }

        void NoEasyOut(
            BinaryOperatorKind kind,
            BoundExpression left,
            BoundExpression right,
            BinaryOperatorOverloadResolutionResult result) {
            var hadApplicableCandidates = false;
            var leftOperatorSource = left.Type()?.StrippedType();
            var rightOperatorSource = right.Type()?.StrippedType();

            if (leftOperatorSource is not null) {
                hadApplicableCandidates = GetUserDefinedOperators(
                    kind,
                    leftOperatorSource,
                    left,
                    right,
                    result.results
                );

                if (!hadApplicableCandidates)
                    result.results.Clear();
            }

            var isShift = kind.IsShift();

            if (!isShift && rightOperatorSource is not null && !rightOperatorSource.Equals(leftOperatorSource)) {
                var rightOperators = ArrayBuilder<BinaryOperatorAnalysisResult>.GetInstance();

                if (GetUserDefinedOperators(kind, rightOperatorSource, left, right, rightOperators)) {
                    hadApplicableCandidates = true;
                    AddDistinctOperators(result.results, rightOperators);
                }

                rightOperators.Free();
            }

            if (!hadApplicableCandidates) {
                result.results.Clear();
                var operators = ArrayBuilder<BinaryOperatorSignature>.GetInstance();
                CorLibrary.GetAllBuiltInBinaryOperators(kind, operators);
                GetEnumOperations(kind, left, right, operators);
                CandidateOperators(operators, left, right, result.results);
                operators.Free();
            }

            BinaryOperatorOverloadResolution(left, right, result);
        }
    }

    internal void UnaryOperatorOverloadResolution(
        UnaryOperatorKind kind,
        BoundExpression operand,
        UnaryOperatorOverloadResolutionResult result) {
        UnaryOperatorEasyOut(kind, operand, result);

        if (result.results.Count > 0)
            return;

        NoEasyOut(kind, operand, result);

        void NoEasyOut(
            UnaryOperatorKind kind,
            BoundExpression operand,
            UnaryOperatorOverloadResolutionResult result) {
            var hadApplicableCandidates = GetUserDefinedOperators(kind, operand, result.results);

            if (!hadApplicableCandidates) {
                result.results.Clear();
                var operators = ArrayBuilder<UnaryOperatorSignature>.GetInstance();
                CorLibrary.GetAllBuiltInUnaryOperators(kind, operators);
                GetEnumOperations(kind, operand, operators);
                CandidateOperators(operators, operand, result.results);
                operators.Free();
            }

            UnaryOperatorOverloadResolution(operand, result);
        }
    }

    internal void MethodOverloadResolution<T>(
        ArrayBuilder<T> members,
        ArrayBuilder<TypeOrConstant> templateArguments,
        BoundExpression receiver,
        AnalyzedArguments arguments,
        OverloadResolutionResult<T> result,
        bool isMethodGroupConversion = false,
        RefKind returnRefKind = default,
        TypeSymbol returnType = null)
        where T : Symbol {
        var results = result.resultsBuilder;

        var checkOverriddenOrHidden = !members.All(
            static m => m.containingSymbol is NamedTypeSymbol { baseType.specialType: SpecialType.Object }
        );

        PerformMemberOverloadResolution(
            results,
            members,
            templateArguments,
            receiver,
            arguments,
            completeResults: false,
            isMethodGroupConversion,
            returnRefKind,
            returnType,
            checkOverriddenOrHidden: checkOverriddenOrHidden
        );

        if (!SingleValidResult(results)) {
            result.Clear();

            PerformMemberOverloadResolution(
                results,
                members,
                templateArguments,
                receiver,
                arguments,
                completeResults: true,
                isMethodGroupConversion,
                returnRefKind,
                returnType,
                checkOverriddenOrHidden: checkOverriddenOrHidden
            );
        }
    }

    internal void ObjectCreationOverloadResolution(
        ImmutableArray<MethodSymbol> constructors,
        AnalyzedArguments arguments,
        OverloadResolutionResult<MethodSymbol> result) {
        var results = result.resultsBuilder;

        PerformObjectCreationOverloadResolution(results, constructors, arguments, false);

        if (!SingleValidResult(results)) {
            result.Clear();

            PerformObjectCreationOverloadResolution(results, constructors, arguments, true);
        }
    }

    private void GetEnumOperations(
        UnaryOperatorKind kind,
        BoundExpression operand,
        ArrayBuilder<UnaryOperatorSignature> operators) {
        var enumType = operand.type;

        if (enumType is null)
            return;

        enumType = enumType.StrippedType();

        if (!enumType.IsValidEnumType())
            return;

        var nullableEnum = CorLibrary.GetOrCreateNullableType(enumType);

        switch (kind) {
            case UnaryOperatorKind.PostfixIncrement:
            case UnaryOperatorKind.PostfixDecrement:
            case UnaryOperatorKind.PrefixIncrement:
            case UnaryOperatorKind.PrefixDecrement:
            case UnaryOperatorKind.BitwiseComplement:
                operators.Add(new UnaryOperatorSignature(kind | UnaryOperatorKind.Enum, enumType, enumType));
                operators.Add(new UnaryOperatorSignature(kind | UnaryOperatorKind.Lifted | UnaryOperatorKind.Enum, nullableEnum, nullableEnum));
                break;
        }
    }

    private void GetEnumOperations(BinaryOperatorKind kind, BoundExpression left, BoundExpression right, ArrayBuilder<BinaryOperatorSignature> results) {
        switch (kind) {
            case BinaryOperatorKind.Multiplication:
            case BinaryOperatorKind.Division:
            case BinaryOperatorKind.Modulo:
            case BinaryOperatorKind.RightShift:
            case BinaryOperatorKind.UnsignedRightShift:
            case BinaryOperatorKind.LeftShift:
            case BinaryOperatorKind.ConditionalAnd:
            case BinaryOperatorKind.ConditionalOr:
                return;
        }

        var leftType = left.type;

        if (leftType is not null)
            leftType = leftType.StrippedType();

        var rightType = right.type;

        if (rightType is not null)
            rightType = rightType.StrippedType();

        bool useIdentityConversion;
        switch (kind) {
            case BinaryOperatorKind.And:
            case BinaryOperatorKind.Or:
            case BinaryOperatorKind.Xor:
                useIdentityConversion = false;
                break;
            case BinaryOperatorKind.Addition:
                useIdentityConversion = true;
                break;
            case BinaryOperatorKind.Subtraction:
                useIdentityConversion = true;
                break;
            case BinaryOperatorKind.Equal:
            case BinaryOperatorKind.NotEqual:
            case BinaryOperatorKind.GreaterThan:
            case BinaryOperatorKind.LessThan:
            case BinaryOperatorKind.GreaterThanOrEqual:
            case BinaryOperatorKind.LessThanOrEqual:
                useIdentityConversion = true;
                break;
            default:
                throw ExceptionUtilities.UnexpectedValue(kind);
        }

        if (leftType is not null)
            GetEnumOperation(kind, leftType, right, results);

        if (rightType is not null && (leftType is null ||
            !(useIdentityConversion ? Conversions.HasIdentityConversion(rightType, leftType) : rightType.Equals(leftType)))) {
            GetEnumOperation(kind, rightType, right, results);
        }
    }


    private void GetEnumOperation(
        BinaryOperatorKind kind,
        TypeSymbol enumType,
        BoundExpression right,
        ArrayBuilder<BinaryOperatorSignature> operators) {
        if (!enumType.IsValidEnumType())
            return;

        var underlying = enumType.GetEnumUnderlyingType();

        var nullableEnum = CorLibrary.GetOrCreateNullableType(enumType);
        var nullableUnderlying = CorLibrary.GetOrCreateNullableType(underlying);

        switch (kind) {
            case BinaryOperatorKind.Addition:
                operators.Add(new BinaryOperatorSignature(BinaryOperatorKind.EnumAndUnderlyingAddition, enumType, underlying, enumType));
                operators.Add(new BinaryOperatorSignature(BinaryOperatorKind.UnderlyingAndEnumAddition, underlying, enumType, enumType));
                operators.Add(new BinaryOperatorSignature(BinaryOperatorKind.LiftedEnumAndUnderlyingAddition, nullableEnum, nullableUnderlying, nullableEnum));
                operators.Add(new BinaryOperatorSignature(BinaryOperatorKind.LiftedUnderlyingAndEnumAddition, nullableUnderlying, nullableEnum, nullableEnum));
                break;
            case BinaryOperatorKind.Subtraction:
                operators.Add(new BinaryOperatorSignature(BinaryOperatorKind.EnumSubtraction, enumType, enumType, underlying));
                operators.Add(new BinaryOperatorSignature(BinaryOperatorKind.EnumAndUnderlyingSubtraction, enumType, underlying, enumType));
                operators.Add(new BinaryOperatorSignature(BinaryOperatorKind.LiftedEnumSubtraction, nullableEnum, nullableEnum, nullableUnderlying));
                operators.Add(new BinaryOperatorSignature(BinaryOperatorKind.LiftedEnumAndUnderlyingSubtraction, nullableEnum, nullableUnderlying, nullableEnum));
                break;
            case BinaryOperatorKind.Equal:
            case BinaryOperatorKind.NotEqual:
            case BinaryOperatorKind.GreaterThan:
            case BinaryOperatorKind.LessThan:
            case BinaryOperatorKind.GreaterThanOrEqual:
            case BinaryOperatorKind.LessThanOrEqual:
                var boolean = CorLibrary.GetSpecialType(SpecialType.Bool);
                operators.Add(new BinaryOperatorSignature(kind | BinaryOperatorKind.Enum, enumType, enumType, boolean));
                operators.Add(new BinaryOperatorSignature(kind | BinaryOperatorKind.Lifted | BinaryOperatorKind.Enum, nullableEnum, nullableEnum, boolean));
                break;
            case BinaryOperatorKind.And:
            case BinaryOperatorKind.Or:
            case BinaryOperatorKind.Xor:
                operators.Add(new BinaryOperatorSignature(kind | BinaryOperatorKind.Enum, enumType, enumType, enumType));
                operators.Add(new BinaryOperatorSignature(kind | BinaryOperatorKind.Lifted | BinaryOperatorKind.Enum, nullableEnum, nullableEnum, nullableEnum));
                break;
        }
    }

    private void BinaryOperatorOverloadResolution(
        BoundExpression left,
        BoundExpression right,
        BinaryOperatorOverloadResolutionResult result) {
        if (result.SingleValid())
            return;

        var candidates = result.results;
        RemoveLowerPriorityMembers<BinaryOperatorAnalysisResult, MethodSymbol>(candidates);

        var bestIndex = GetTheBestCandidateIndex(left, right, candidates);

        if (bestIndex != -1) {
            for (var index = 0; index < candidates.Count; index++) {
                if (candidates[index].kind != OperatorAnalysisResultKind.Inapplicable && index != bestIndex)
                    candidates[index] = candidates[index].Worse();
            }

            return;
        }

        for (var i = 1; i < candidates.Count; ++i) {
            if (candidates[i].kind != OperatorAnalysisResultKind.Applicable)
                continue;

            for (var j = 0; j < i; j++) {
                if (candidates[j].kind == OperatorAnalysisResultKind.Inapplicable)
                    continue;

                var better = BetterOperator(candidates[i].signature, candidates[j].signature, left, right);

                if (better == BetterResult.Left)
                    candidates[j] = candidates[j].Worse();
                else if (better == BetterResult.Right)
                    candidates[i] = candidates[i].Worse();
            }
        }
    }

    private void UnaryOperatorOverloadResolution(
        BoundExpression operand,
        UnaryOperatorOverloadResolutionResult result) {
        if (result.SingleValid())
            return;

        var candidates = result.results;
        RemoveLowerPriorityMembers<UnaryOperatorAnalysisResult, MethodSymbol>(candidates);

        var bestIndex = GetTheBestCandidateIndex(operand, candidates);

        if (bestIndex != -1) {
            for (var index = 0; index < candidates.Count; index++) {
                if (candidates[index].kind != OperatorAnalysisResultKind.Inapplicable && index != bestIndex)
                    candidates[index] = candidates[index].Worse();
            }

            return;
        }

        for (var i = 1; i < candidates.Count; i++) {
            if (candidates[i].kind != OperatorAnalysisResultKind.Applicable)
                continue;

            for (var j = 0; j < i; j++) {
                if (candidates[j].kind == OperatorAnalysisResultKind.Inapplicable)
                    continue;

                var better = BetterOperator(candidates[i].signature, candidates[j].signature, operand);

                if (better == BetterResult.Left)
                    candidates[j] = candidates[j].Worse();
                else if (better == BetterResult.Right)
                    candidates[i] = candidates[i].Worse();
            }
        }
    }

    private int GetTheBestCandidateIndex(
        BoundExpression operand,
        ArrayBuilder<UnaryOperatorAnalysisResult> candidates) {
        var currentBestIndex = -1;

        for (var index = 0; index < candidates.Count; index++) {
            if (candidates[index].kind != OperatorAnalysisResultKind.Applicable)
                continue;

            if (currentBestIndex == -1) {
                currentBestIndex = index;
            } else {
                var better = BetterOperator(
                    candidates[currentBestIndex].signature,
                    candidates[index].signature,
                    operand
                );

                if (better == BetterResult.Right)
                    currentBestIndex = index;
                else if (better != BetterResult.Left)
                    currentBestIndex = -1;
            }
        }

        for (var index = 0; index < currentBestIndex; index++) {
            if (candidates[index].kind == OperatorAnalysisResultKind.Inapplicable)
                continue;

            var better = BetterOperator(
                candidates[currentBestIndex].signature,
                candidates[index].signature,
                operand
            );

            if (better != BetterResult.Left)
                return -1;
        }

        return currentBestIndex;
    }

    private BetterResult BetterOperator(
        UnaryOperatorSignature op1,
        UnaryOperatorSignature op2,
        BoundExpression operand) {
        var better = BetterConversionFromExpression(operand, op1.operandType, op2.operandType);

        if (better == BetterResult.Left || better == BetterResult.Right)
            return better;

        if (Conversions.HasIdentityConversion(op1.operandType, op2.operandType)) {
            var lifted1 = op1.kind.IsLifted();
            var lifted2 = op2.kind.IsLifted();

            if (lifted1 && !lifted2)
                return BetterResult.Right;
            else if (!lifted1 && lifted2)
                return BetterResult.Left;
        }

        return BetterResult.Neither;
    }

    private bool GetUserDefinedOperators(
        UnaryOperatorKind kind,
        BoundExpression operand,
        ArrayBuilder<UnaryOperatorAnalysisResult> results) {
        if (operand.Type() is null)
            return false;

        var type0 = operand.StrippedType();
        TypeSymbol constrainedToTypeOpt = type0 as TemplateParameterSymbol;

        if (OperatorFacts.NoUserDefinedOperators(type0))
            return false;

        var operators = ArrayBuilder<UnaryOperatorSignature>.GetInstance();
        var hadApplicableCandidates = false;

        if (type0 is not NamedTypeSymbol current)
            current = type0.baseType;

        if (current is null && type0.IsTemplateParameter())
            current = ((TemplateParameterSymbol)type0).effectiveBaseClass;

        for (; current is not null; current = current.baseType) {
            operators.Clear();

            GetUserDefinedUnaryOperatorsFromType(constrainedToTypeOpt, current, kind, operators);

            results.Clear();

            if (CandidateOperators(operators, operand, results)) {
                hadApplicableCandidates = true;
                break;
            }
        }

        operators.Free();
        return hadApplicableCandidates;
    }

    private void GetUserDefinedUnaryOperatorsFromType(
        TypeSymbol constrainedToTypeOpt,
        NamedTypeSymbol type,
        UnaryOperatorKind kind,
        ArrayBuilder<UnaryOperatorSignature> operators) {
        var name1 = OperatorFacts.GetUnaryOperatorNameFromKind(kind);

        GetDeclaredOperators(constrainedToTypeOpt, type, kind, name1, operators);
        AddLiftedOperators(constrainedToTypeOpt, kind, operators);

        static void GetDeclaredOperators(
            TypeSymbol constrainedToTypeOpt,
            NamedTypeSymbol type,
            UnaryOperatorKind kind,
            string name,
            ArrayBuilder<UnaryOperatorSignature> operators) {
            var typeOperators = ArrayBuilder<MethodSymbol>.GetInstance();
            type.AddOperators(name, typeOperators);

            foreach (var op in typeOperators) {
                if (op.parameterCount != 1 || op.returnsVoid)
                    continue;

                var operandType = op.GetParameterType(0);
                var resultType = op.returnType;

                operators.Add(new UnaryOperatorSignature(
                    UnaryOperatorKind.UserDefined | kind,
                    operandType,
                    resultType,
                    op,
                    constrainedToTypeOpt
                ));
            }

            typeOperators.Free();
        }

        void AddLiftedOperators(
            TypeSymbol constrainedToTypeOpt,
            UnaryOperatorKind kind,
            ArrayBuilder<UnaryOperatorSignature> operators) {
            switch (kind) {
                case UnaryOperatorKind.UnaryPlus:
                case UnaryOperatorKind.PrefixDecrement:
                case UnaryOperatorKind.PrefixIncrement:
                case UnaryOperatorKind.UnaryMinus:
                case UnaryOperatorKind.PostfixDecrement:
                case UnaryOperatorKind.PostfixIncrement:
                case UnaryOperatorKind.LogicalNegation:
                case UnaryOperatorKind.BitwiseComplement:
                    for (var i = operators.Count - 1; i >= 0; i--) {
                        var op = operators[i].method;
                        var operandType = op.GetParameterType(0);
                        var resultType = op.returnType;

                        if (!operandType.IsNullableType() &&
                            !resultType.IsNullableType()) {
                            operators.Add(new UnaryOperatorSignature(
                                UnaryOperatorKind.Lifted | UnaryOperatorKind.UserDefined | kind,
                                MakeNullable(operandType), MakeNullable(resultType), op, constrainedToTypeOpt));
                        }
                    }

                    break;
            }
        }
    }

    private BetterResult BetterOperator(
        BinaryOperatorSignature op1,
        BinaryOperatorSignature op2,
        BoundExpression left,
        BoundExpression right) {
        var leftBetter = BetterConversionFromExpression(left, op1.leftType, op2.leftType);
        var rightBetter = BetterConversionFromExpression(right, op1.rightType, op2.rightType);

        if (leftBetter == BetterResult.Left && rightBetter != BetterResult.Right ||
            leftBetter != BetterResult.Right && rightBetter == BetterResult.Left) {
            return BetterResult.Left;
        }

        if (leftBetter == BetterResult.Right && rightBetter != BetterResult.Left ||
            leftBetter != BetterResult.Left && rightBetter == BetterResult.Right) {
            return BetterResult.Right;
        }

        if (Conversions.HasIdentityConversion(op1.leftType, op2.leftType) &&
            Conversions.HasIdentityConversion(op1.rightType, op2.rightType)) {
            var result = MoreSpecificOperator(op1, op2);

            if (result == BetterResult.Left || result == BetterResult.Right)
                return result;

            var lifted1 = op1.kind.IsLifted();
            var lifted2 = op2.kind.IsLifted();

            if (lifted1 && !lifted2)
                return BetterResult.Right;
            else if (!lifted1 && lifted2)
                return BetterResult.Left;
        }

        return BetterResult.Neither;
    }

    private int GetTheBestCandidateIndex(
        BoundExpression left,
        BoundExpression right,
        ArrayBuilder<BinaryOperatorAnalysisResult> candidates) {
        var currentBestIndex = -1;

        for (var index = 0; index < candidates.Count; index++) {
            if (candidates[index].kind != OperatorAnalysisResultKind.Applicable)
                continue;

            if (currentBestIndex == -1) {
                currentBestIndex = index;
            } else {
                var better = BetterOperator(
                    candidates[currentBestIndex].signature,
                    candidates[index].signature,
                    left,
                    right
                );

                if (better == BetterResult.Right)
                    currentBestIndex = index;
                else if (better != BetterResult.Left)
                    currentBestIndex = -1;
            }
        }

        for (var index = 0; index < currentBestIndex; index++) {
            if (candidates[index].kind == OperatorAnalysisResultKind.Inapplicable)
                continue;

            var better = BetterOperator(
                candidates[currentBestIndex].signature,
                candidates[index].signature,
                left,
                right
            );

            if (better != BetterResult.Left)
                return -1;
        }

        return currentBestIndex;
    }

    private BetterResult MoreSpecificOperator(BinaryOperatorSignature op1, BinaryOperatorSignature op2) {
        TypeSymbol op1Left, op1Right, op2Left, op2Right;

        if (op1.method is not null) {
            var p = op1.method.originalDefinition.GetParameters();
            op1Left = p[0].type;
            op1Right = p[1].type;

            if (op1.kind.IsLifted()) {
                op1Left = MakeNullable(op1Left);
                op1Right = MakeNullable(op1Right);
            }
        } else {
            op1Left = op1.leftType;
            op1Right = op1.rightType;
        }

        if (op2.method is not null) {
            var p = op2.method.originalDefinition.GetParameters();
            op2Left = p[0].type;
            op2Right = p[1].type;

            if (op2.kind.IsLifted()) {
                op2Left = MakeNullable(op2Left);
                op2Right = MakeNullable(op2Right);
            }
        } else {
            op2Left = op2.leftType;
            op2Right = op2.rightType;
        }

        using var uninst1 = TemporaryArray<TypeSymbol>.Empty;
        using var uninst2 = TemporaryArray<TypeSymbol>.Empty;

        uninst1.Add(op1Left);
        uninst1.Add(op1Right);

        uninst2.Add(op2Left);
        uninst2.Add(op2Right);

        var result = MoreSpecificType(ref uninst1.AsRef(), ref uninst2.AsRef());

        return result;
    }

    private bool GetUserDefinedOperators(
        BinaryOperatorKind kind,
        TypeSymbol type0,
        BoundExpression left,
        BoundExpression right,
        ArrayBuilder<BinaryOperatorAnalysisResult> results) {
        if (type0 is null || OperatorFacts.NoUserDefinedOperators(type0))
            return false;

        var operators = ArrayBuilder<BinaryOperatorSignature>.GetInstance();
        var hadApplicableCandidates = false;

        if (type0 is not NamedTypeSymbol current)
            current = type0.baseType;

        if (current is null && type0.IsTemplateParameter())
            current = ((TemplateParameterSymbol)type0).effectiveBaseClass;

        for (; current is not null; current = current.baseType) {
            operators.Clear();
            GetUserDefinedBinaryOperatorsFromType(null, current, kind, operators);
            results.Clear();

            if (CandidateOperators(operators, left, right, results)) {
                hadApplicableCandidates = true;
                break;
            }
        }

        operators.Free();
        return hadApplicableCandidates;
    }

    private void GetUserDefinedBinaryOperatorsFromType(
        TypeSymbol constrainedToTypeOpt,
        NamedTypeSymbol type,
        BinaryOperatorKind kind,
        ArrayBuilder<BinaryOperatorSignature> operators) {
        var name1 = OperatorFacts.GetBinaryOperatorNameFromKind(kind);

        GetDeclaredOperators(constrainedToTypeOpt, type, kind, name1, operators);
        AddLiftedOperators(constrainedToTypeOpt, kind, operators);

        void GetDeclaredOperators(
            TypeSymbol constrainedToTypeOpt,
            NamedTypeSymbol type,
            BinaryOperatorKind kind,
            string name,
            ArrayBuilder<BinaryOperatorSignature> operators) {
            var typeOperators = ArrayBuilder<MethodSymbol>.GetInstance();
            type.AddOperators(name, typeOperators);

            foreach (var op in typeOperators) {
                if (op.parameterCount != 2 || op.returnsVoid)
                    continue;

                var leftOperandType = op.GetParameterType(0);
                var rightOperandType = op.GetParameterType(1);
                var resultType = op.returnType;

                operators.Add(new BinaryOperatorSignature(
                    BinaryOperatorKind.UserDefined | kind,
                    leftOperandType,
                    rightOperandType,
                    resultType,
                    op,
                    constrainedToTypeOpt
                ));
            }

            typeOperators.Free();
        }

        void AddLiftedOperators(
            TypeSymbol constrainedToTypeOpt,
            BinaryOperatorKind kind,
            ArrayBuilder<BinaryOperatorSignature> operators) {
            for (var i = operators.Count - 1; i >= 0; i--) {
                var op = operators[i].method;
                var leftOperandType = op.GetParameterType(0);
                var rightOperandType = op.GetParameterType(1);
                var resultType = op.returnType;

                var lifting = UserDefinedBinaryOperatorCanBeLifted(
                    leftOperandType,
                    rightOperandType,
                    resultType,
                    kind
                );

                if (lifting == LiftingResult.LiftOperandsAndResult) {
                    operators.Add(new BinaryOperatorSignature(
                        BinaryOperatorKind.Lifted | BinaryOperatorKind.UserDefined | kind,
                        MakeNullable(leftOperandType),
                        MakeNullable(rightOperandType),
                        MakeNullable(resultType),
                        op,
                        constrainedToTypeOpt
                    ));
                } else if (lifting == LiftingResult.LiftOperandsButNotResult) {
                    operators.Add(new BinaryOperatorSignature(
                        BinaryOperatorKind.Lifted | BinaryOperatorKind.UserDefined | kind,
                        MakeNullable(leftOperandType),
                        MakeNullable(rightOperandType),
                        resultType,
                        op,
                        constrainedToTypeOpt
                    ));
                }
            }
        }
    }

    private NamedTypeSymbol MakeNullable(TypeSymbol type) {
        return CorLibrary.GetSpecialType(SpecialType.Nullable).Construct([new TypeOrConstant(type)]);
    }

    private static LiftingResult UserDefinedBinaryOperatorCanBeLifted(
        TypeSymbol left,
        TypeSymbol right,
        TypeSymbol result,
        BinaryOperatorKind kind) {
        if (left.IsNullableType() || right.IsNullableType())
            return LiftingResult.NotLifted;

        switch (kind) {
            case BinaryOperatorKind.Equal:
            case BinaryOperatorKind.NotEqual:
                if (!TypeSymbol.Equals(left, right, TypeCompareKind.ConsiderEverything))
                    return LiftingResult.NotLifted;

                goto case BinaryOperatorKind.GreaterThan;
            case BinaryOperatorKind.GreaterThan:
            case BinaryOperatorKind.GreaterThanOrEqual:
            case BinaryOperatorKind.LessThan:
            case BinaryOperatorKind.LessThanOrEqual:
                return result.specialType == SpecialType.Bool
                    ? LiftingResult.LiftOperandsButNotResult
                    : LiftingResult.NotLifted;
            default:
                return result.IsNullableType() ? LiftingResult.NotLifted : LiftingResult.LiftOperandsAndResult;
        }
    }

    private static void AddDistinctOperators(
        ArrayBuilder<BinaryOperatorAnalysisResult> result,
        ArrayBuilder<BinaryOperatorAnalysisResult> additionalOperators) {
        var initialCount = result.Count;

        foreach (var op in additionalOperators) {
            var equivalentToExisting = false;

            for (var i = 0; i < initialCount; i++) {
                var existingSignature = result[i].signature;

                if (op.signature.kind == existingSignature.kind &&
                    EqualsIgnoringNullable(op.signature.returnType, existingSignature.returnType) &&
                    EqualsIgnoringNullable(op.signature.leftType, existingSignature.leftType) &&
                    EqualsIgnoringNullable(op.signature.rightType, existingSignature.rightType) &&
                    EqualsIgnoringNullable(
                        op.signature.method.containingType,
                        existingSignature.method.containingType)) {
                    equivalentToExisting = true;
                    break;
                }
            }

            if (!equivalentToExisting)
                result.Add(op);
        }

        static bool EqualsIgnoringNullable(TypeSymbol a, TypeSymbol b)
            => a.Equals(b, TypeCompareKind.IgnoreNullability);
    }

    private void PerformObjectCreationOverloadResolution(
        ArrayBuilder<MemberResolutionResult<MethodSymbol>> results,
        ImmutableArray<MethodSymbol> constructors,
        AnalyzedArguments arguments,
        bool completeResults) {
        foreach (var constructor in constructors)
            AddConstructorToCandidateSet(constructor, results, arguments, completeResults);

        RemoveLowerPriorityMembers<MemberResolutionResult<MethodSymbol>, MethodSymbol>(results);
        RemoveWorseMembers(results, arguments);

        return;
    }

    private void AddConstructorToCandidateSet(
        MethodSymbol constructor,
        ArrayBuilder<MemberResolutionResult<MethodSymbol>> results,
        AnalyzedArguments arguments,
        bool completeResults) {
        var normalResult = IsConstructorApplicableInNormalForm(constructor, arguments, completeResults);
        var result = normalResult;

        if (result.isValid || completeResults)
            results.Add(new MemberResolutionResult<MethodSymbol>(constructor, constructor, result, false));
    }

    private MemberAnalysisResult IsConstructorApplicableInNormalForm(
        MethodSymbol constructor,
        AnalyzedArguments arguments,
        bool completeResults) {
        var argumentAnalysis = AnalyzeArguments(
            constructor.GetParameters().ToImmutableArray<Symbol>(),
            arguments,
            isMethodGroupConversion: false,
            false
        );

        if (!argumentAnalysis.isValid)
            return MemberAnalysisResult.ArgumentParameterMismatch(argumentAnalysis);

        var effectiveParameters = GetEffectiveParametersInNormalForm(
            constructor,
            arguments.arguments.Count,
            argumentAnalysis.argsToParams,
            arguments.refKinds,
            hasAnyRefOmittedArgument: out _
        );

        return IsApplicable(
            constructor,
            effectiveParameters,
            arguments,
            argumentAnalysis.argsToParams,
            hasAnyRefOmittedArgument: false,
            completeResults: completeResults
        );
    }

    private bool CandidateOperators(
        ArrayBuilder<BinaryOperatorSignature> operators,
        BoundExpression left,
        BoundExpression right,
        ArrayBuilder<BinaryOperatorAnalysisResult> results) {
        var hadApplicableCandidate = false;

        foreach (var op in operators) {
            var convLeft = conversions.ClassifyConversionFromExpression(left, op.leftType);
            var convRight = conversions.ClassifyConversionFromExpression(right, op.rightType);

            if (IsImplicitConversion(convLeft) && IsImplicitConversion(convRight)) {
                results.Add(BinaryOperatorAnalysisResult.Applicable(op, convLeft, convRight));
                hadApplicableCandidate = true;
            } else {
                results.Add(BinaryOperatorAnalysisResult.Inapplicable(op, convLeft, convRight));
            }
        }

        return hadApplicableCandidate;
    }

    private bool IsImplicitConversion(Conversion conversion) {
        if (!conversion.isImplicit)
            return false;

        if (conversion.underlyingConversions != default) {
            if (!conversion.underlyingConversions[0].isImplicit)
                return false;
        }

        return true;
    }

    private bool CandidateOperators(
        ArrayBuilder<UnaryOperatorSignature> operators,
        BoundExpression operand,
        ArrayBuilder<UnaryOperatorAnalysisResult> results) {
        var hadApplicableCandidate = false;

        foreach (var op in operators) {
            var conversion = conversions.ClassifyConversionFromExpression(operand, op.operandType);

            if (conversion.isImplicit) {
                results.Add(UnaryOperatorAnalysisResult.Applicable(op, conversion));
                hadApplicableCandidate = true;
            } else {
                results.Add(UnaryOperatorAnalysisResult.Inapplicable(op, conversion));
            }
        }

        return hadApplicableCandidate;
    }

    private void PerformMemberOverloadResolution<T>(
        ArrayBuilder<MemberResolutionResult<T>> results,
        ArrayBuilder<T> members,
        ArrayBuilder<TypeOrConstant> templateArguments,
        BoundExpression receiver,
        AnalyzedArguments arguments,
        bool completeResults,
        bool isMethodGroupConversion,
        RefKind returnRefKind,
        TypeSymbol returnType,
        bool checkOverriddenOrHidden)
        where T : Symbol {
        Dictionary<NamedTypeSymbol, ArrayBuilder<T>> containingTypeMap = null;

        if (checkOverriddenOrHidden && members.Count > 50)
            containingTypeMap = PartitionMembersByContainingType(members);

        for (var i = 0; i < members.Count; i++) {
            AddMemberToCandidateSet(
                members[i],
                results,
                members,
                templateArguments,
                arguments,
                completeResults,
                containingTypeMap,
                checkOverriddenOrHidden: checkOverriddenOrHidden
            );
        }

        ClearContainingTypeMap(ref containingTypeMap);
        RemoveInaccessibleTypeArguments(results);

        if (checkOverriddenOrHidden)
            RemoveLessDerivedMembers(results);

        RemoveStaticInstanceMismatches(results, arguments, receiver);
        RemoveConstraintViolations(results);

        if (isMethodGroupConversion)
            RemoveFunctionConversionsWithWrongReturnType(results, returnRefKind, returnType);

        if (!AnyValidResult(results))
            return;

        RemoveLowerPriorityMembers<MemberResolutionResult<T>, T>(results);
        RemoveWorseMembers(results, arguments);
    }

    private void RemoveFunctionConversionsWithWrongReturnType<TMember>(
        ArrayBuilder<MemberResolutionResult<TMember>> results,
        RefKind? returnRefKind,
        TypeSymbol returnType) where TMember : Symbol {
        for (var f = 0; f < results.Count; ++f) {
            var result = results[f];

            if (!result.result.isValid)
                continue;

            var method = (MethodSymbol)(Symbol)result.member;
            bool returnsMatch;

            if (returnType is null || method.returnType.Equals(returnType, TypeCompareKind.AllIgnoreOptions))
                returnsMatch = true;
            else if (returnRefKind == RefKind.None)
                returnsMatch = conversions.HasIdentityOrImplicitReferenceConversion(method.returnType, returnType);
            else
                returnsMatch = false;

            if (!returnsMatch)
                results[f] = result.WithResult(MemberAnalysisResult.WrongReturnType());
            else if (method.refKind != returnRefKind)
                results[f] = result.WithResult(MemberAnalysisResult.WrongRefKind());
        }
    }

    private void RemoveConstraintViolations<TMember>(ArrayBuilder<MemberResolutionResult<TMember>> results)
        where TMember : Symbol {
        if (typeof(TMember) != typeof(MethodSymbol))
            return;

        // TODO template constraints
        // for (var f = 0; f < results.Count; ++f) {
        //     var result = results[f];
        //     var member = (MethodSymbol)(Symbol)result.member;

        //     if ((result.result.isValid || result.result.kind == MemberResolutionKind.ConstructedParameterFailedConstraintCheck) &&
        //         FailsConstraintChecks(member, out ArrayBuilder<TypeParameterDiagnosticInfo> constraintFailureDiagnosticsOpt, template)) {
        //         results[f] = result.WithResult(
        //             MemberAnalysisResult.ConstraintFailure(constraintFailureDiagnosticsOpt.ToImmutableAndFree()));
        //     }
        // }
    }

    private void RemoveStaticInstanceMismatches<TMember>(
        ArrayBuilder<MemberResolutionResult<TMember>> results,
        AnalyzedArguments arguments,
        BoundExpression receiverOpt) where TMember : Symbol {
        var isImplicitReceiver = Binder.WasImplicitReceiver(receiverOpt);
        var isStaticContext = !_binder.HasThis(!isImplicitReceiver, out var inStaticContext) || inStaticContext;

        if (isImplicitReceiver && !isStaticContext)
            return;

        var keepStatic = isImplicitReceiver && isStaticContext || Binder.IsMemberAccessedThroughType(receiverOpt);

        RemoveStaticInstanceMismatches(results, keepStatic);
    }

    private static void RemoveStaticInstanceMismatches<TMember>(
        ArrayBuilder<MemberResolutionResult<TMember>> results,
        bool requireStatic)
        where TMember : Symbol {
        for (var f = 0; f < results.Count; f++) {
            var result = results[f];
            var member = result.member;

            if (result.result.isValid && member.RequiresInstanceReceiver() == requireStatic)
                results[f] = result.WithResult(MemberAnalysisResult.StaticInstanceMismatch());
        }
    }

    private void AddMemberToCandidateSet<TMember>(
        TMember member,
        ArrayBuilder<MemberResolutionResult<TMember>> results,
        ArrayBuilder<TMember> members,
        ArrayBuilder<TypeOrConstant> templateArguments,
        AnalyzedArguments arguments,
        bool completeResults,
        Dictionary<NamedTypeSymbol, ArrayBuilder<TMember>> containingTypeMap,
        bool checkOverriddenOrHidden = true)
        where TMember : Symbol {
        if (checkOverriddenOrHidden) {
            if (members.Count < 2) {
            } else if (containingTypeMap is null) {
                if (MemberGroupContainsMoreDerivedOverride(members, member, checkOverrideContainingType: true))
                    return;

                if (MemberGroupHidesByName(members, member))
                    return;
            } else if (containingTypeMap.Count == 1) {
            } else {
                var memberContainingType = member.containingType;

                foreach (var pair in containingTypeMap) {
                    var otherType = pair.Key;

                    if (otherType.IsDerivedFrom(memberContainingType, TypeCompareKind.ConsiderEverything)) {
                        var others = pair.Value;

                        if (MemberGroupContainsMoreDerivedOverride(others, member, checkOverrideContainingType: false))
                            return;

                        if (MemberGroupHidesByName(others, member))
                            return;
                    }
                }
            }
        }

        var leastOverriddenMember = (TMember)member.GetLeastOverriddenMember(_binder.containingType);

        var result = IsMemberApplicableInNormalForm(
            member,
            leastOverriddenMember,
            templateArguments,
            arguments,
            completeResults: completeResults
        );

        if (result.result.isValid || completeResults)
            results.Add(result);
    }

    private static void RemoveLessDerivedMembers<TMember>(ArrayBuilder<MemberResolutionResult<TMember>> results)
        where TMember : Symbol {
        for (var f = 0; f < results.Count; f++) {
            var result = results[f];

            if (!result.result.isValid)
                continue;

            if (IsLessDerivedThanAny(index: f, result.leastOverriddenMember.containingType, results))
                results[f] = result.WithResult(MemberAnalysisResult.LessDerived());
        }
    }

    private static bool IsLessDerivedThanAny<TMember>(
        int index,
        TypeSymbol type,
        ArrayBuilder<MemberResolutionResult<TMember>> results)
        where TMember : Symbol {
        for (var f = 0; f < results.Count; f++) {
            if (f == index)
                continue;

            var result = results[f];

            if (!result.result.isValid)
                continue;

            var currentType = result.leastOverriddenMember.containingType;

            if (type.specialType == SpecialType.Object && currentType.specialType != SpecialType.Object)
                return true;

            if (currentType.IsClassType() &&
                type.IsClassType() &&
                currentType.IsDerivedFrom(type, TypeCompareKind.ConsiderEverything)) {
                return true;
            }
        }

        return false;
    }

    private void RemoveInaccessibleTypeArguments<TMember>(ArrayBuilder<MemberResolutionResult<TMember>> results)
        where TMember : Symbol {
        for (var f = 0; f < results.Count; f++) {
            var result = results[f];

            if (result.result.isValid && !TemplateArgumentsAccessible(result.member.GetMemberTemplateParameters()))
                results[f] = result.WithResult(MemberAnalysisResult.InaccessibleTemplateArgument());
        }
    }

    private bool TemplateArgumentsAccessible(ImmutableArray<TemplateParameterSymbol> templateArguments) {
        foreach (var arg in templateArguments) {
            if (!_binder.IsAccessible(arg))
                return false;
        }

        return true;
    }

    private void RemoveLowerPriorityMembers<TMemberResolution, TMember>(ArrayBuilder<TMemberResolution> results)
        // where TMemberResolution : IMemberResolutionResultWithPriority<TMember>
        where TMember : Symbol {
        // TODO Do we have priority members?
        // if (results.Count < 2)
        //     return;

        // if (results.All(r => r.MemberWithPriority?.GetOverloadResolutionPriority() is null or 0)) {
        //     // All default, nothing to do
        //     return;
        // }

        // bool removedMembers = false;
        // var resultsByContainingType = PooledDictionary<NamedTypeSymbol, OneOrMany<TMemberResolution>>.GetInstance();

        // foreach (var result in results) {
        //     if (result.MemberWithPriority is null) {
        //         // Can happen for things like built-in binary operators
        //         continue;
        //     }

        //     var containingType = result.MemberWithPriority.ContainingType;
        //     if (resultsByContainingType.TryGetValue(containingType, out var previousResults)) {
        //         var previousPriority = previousResults.First().MemberWithPriority.GetOverloadResolutionPriority();
        //         var currentPriority = result.MemberWithPriority.GetOverloadResolutionPriority();

        //         if (currentPriority > previousPriority) {
        //             removedMembers = true;
        //             resultsByContainingType[containingType] = OneOrMany.Create(result);
        //         } else if (currentPriority == previousPriority) {
        //             resultsByContainingType[containingType] = previousResults.Add(result);
        //         } else {
        //             removedMembers = true;
        //             Debug.Assert(previousResults.All(r => r.MemberWithPriority.GetOverloadResolutionPriority() == previousPriority));
        //         }
        //     } else {
        //         resultsByContainingType.Add(containingType, OneOrMany.Create(result));
        //     }
        // }

        // if (!removedMembers) {
        //     // No changes, so we can just return
        //     resultsByContainingType.Free();
        //     return;
        // }

        // results.Clear();
        // foreach (var (_, resultsForType) in resultsByContainingType) {
        //     results.AddRange(resultsForType);
        // }
        // resultsByContainingType.Free();
    }

    private int GetBestCandidateIndex<TMember>(
        ArrayBuilder<MemberResolutionResult<TMember>> results,
        AnalyzedArguments arguments)
        where TMember : Symbol {
        var currentBestIndex = -1;
        for (var index = 0; index < results.Count; index++) {
            if (!results[index].isValid)
                continue;

            if (currentBestIndex == -1) {
                currentBestIndex = index;
            } else if (results[currentBestIndex].member == results[index].member) {
                currentBestIndex = -1;
            } else {
                var better = BetterFunctionMember(results[currentBestIndex], results[index], arguments.arguments);

                if (better == BetterResult.Right)
                    currentBestIndex = index;
                else if (better != BetterResult.Left)
                    currentBestIndex = -1;
            }
        }

        for (var index = 0; index < currentBestIndex; index++) {
            if (!results[index].isValid)
                continue;

            if (results[currentBestIndex].member == results[index].member)
                return -1;

            var better = BetterFunctionMember(results[currentBestIndex], results[index], arguments.arguments);

            if (better != BetterResult.Left)
                return -1;
        }

        return currentBestIndex;
    }

    private void RemoveWorseMembers<TMember>(
        ArrayBuilder<MemberResolutionResult<TMember>> results,
        AnalyzedArguments arguments)
        where TMember : Symbol {
        if (SingleValidResult(results))
            return;

        var bestIndex = GetBestCandidateIndex(results, arguments);

        if (bestIndex != -1) {
            for (var index = 0; index < results.Count; index++) {
                if (results[index].isValid && index != bestIndex)
                    results[index] = results[index].Worse();
            }

            return;
        }

        const int Unknown = 0;
        const int WorseThanSomething = 1;
        const int NotBetterThanEverything = 2;

        var worse = ArrayBuilder<int>.GetInstance(results.Count, Unknown);

        var countOfNotBestCandidates = 0;
        var notBestIdx = -1;

        for (var c1Idx = 0; c1Idx < results.Count; c1Idx++) {
            var c1Result = results[c1Idx];

            if (!c1Result.isValid || worse[c1Idx] == WorseThanSomething)
                continue;

            for (var c2Idx = 0; c2Idx < results.Count; c2Idx++) {
                var c2Result = results[c2Idx];

                if (!c2Result.isValid || c1Idx == c2Idx || c1Result.member == c2Result.member)
                    continue;

                var better = BetterFunctionMember(c1Result, c2Result, arguments.arguments);

                if (better == BetterResult.Left) {
                    worse[c2Idx] = WorseThanSomething;
                } else if (better == BetterResult.Right) {
                    worse[c1Idx] = WorseThanSomething;
                    break;
                }
            }

            if (worse[c1Idx] == Unknown) {
                worse[c1Idx] = NotBetterThanEverything;
                countOfNotBestCandidates++;
                notBestIdx = c1Idx;
            }
        }

        if (countOfNotBestCandidates == 0) {
            for (var i = 0; i < worse.Count; i++) {
                if (worse[i] == WorseThanSomething)
                    results[i] = results[i].Worse();
            }
        } else if (countOfNotBestCandidates == 1) {
            for (var i = 0; i < worse.Count; i++) {
                if (worse[i] == WorseThanSomething) {
                    results[i] = BetterResult.Left == BetterFunctionMember(results[notBestIdx], results[i], arguments.arguments)
                        ? results[i].Worst() : results[i].Worse();
                }
            }

            results[notBestIdx] = results[notBestIdx].Worse();
        } else {
            for (var i = 0; i < worse.Count; i++) {
                if (worse[i] == WorseThanSomething)
                    results[i] = results[i].Worst();
                else if (worse[i] == NotBetterThanEverything)
                    results[i] = results[i].Worse();
            }
        }

        worse.Free();
    }

    private static void ClearContainingTypeMap<TMember>(
        ref Dictionary<NamedTypeSymbol, ArrayBuilder<TMember>> containingTypeMap)
        where TMember : Symbol {
        if (containingTypeMap != null) {
            foreach (var builder in containingTypeMap.Values)
                builder.Free();

            containingTypeMap = null;
        }
    }

    private BetterResult BetterFunctionMember<TMember>(
        MemberResolutionResult<TMember> m1,
        MemberResolutionResult<TMember> m2,
        ArrayBuilder<BoundExpressionOrTypeOrConstant> arguments)
        where TMember : Symbol {
        switch (RequiredFunctionType(m1), RequiredFunctionType(m2)) {
            case (false, true):
                return BetterResult.Left;
            case (true, false):
                return BetterResult.Right;
        }

        var hasAnyRefOmittedArgument1 = m1.result.hasAnyRefOmittedArgument;
        var hasAnyRefOmittedArgument2 = m2.result.hasAnyRefOmittedArgument;

        if (hasAnyRefOmittedArgument1 != hasAnyRefOmittedArgument2)
            return hasAnyRefOmittedArgument1 ? BetterResult.Right : BetterResult.Left;
        else
            return BetterFunctionMember(m1, m2, arguments, considerRefKinds: hasAnyRefOmittedArgument1);
    }

    private BetterResult BetterFunctionMember<TMember>(
        MemberResolutionResult<TMember> m1,
        MemberResolutionResult<TMember> m2,
        ArrayBuilder<BoundExpressionOrTypeOrConstant> arguments,
        bool considerRefKinds)
        where TMember : Symbol {
        var result = BetterResult.Neither;
        var okToDowngradeResultToNeither = false;
        var ignoreDowngradableToNeither = false;

        var m1LeastOverriddenParameters = m1.leastOverriddenMember.GetParameters();
        var m2LeastOverriddenParameters = m2.leastOverriddenMember.GetParameters();

        var allSame = true;
        int i;
        for (i = 0; i < arguments.Count; i++) {
            var argumentKind = arguments[i].expression.kind;

            var type1 = GetParameterTypeAndRefKind(
                i,
                m1.result,
                m1LeastOverriddenParameters,
                out var parameter1RefKind
            );

            var type2 = GetParameterTypeAndRefKind(
                i,
                m2.result,
                m2LeastOverriddenParameters,
                out var parameter2RefKind
            );

            BetterResult r;

            r = BetterConversionFromExpression(
                arguments[i].expression,
                type1,
                m1.result.ConversionForArg(i),
                parameter1RefKind,
                type2,
                m2.result.ConversionForArg(i),
                parameter2RefKind,
                considerRefKinds,
                out var okToDowngradeToNeither
            );

            var type1Normalized = type1;
            var type2Normalized = type2;

            // type1Normalized = type1.NormalizeTaskTypes(Compilation);
            // type2Normalized = type2.NormalizeTaskTypes(Compilation);

            if (r == BetterResult.Neither) {
                if (allSame && conversions.ClassifyImplicitConversionFromType(type1Normalized, type2Normalized)
                    .kind != ConversionKind.Identity) {
                    allSame = false;
                }

                continue;
            }

            if (conversions.ClassifyImplicitConversionFromType(type1Normalized, type2Normalized).kind
                != ConversionKind.Identity) {
                allSame = false;
            }

            if (result == BetterResult.Neither) {
                if (!(ignoreDowngradableToNeither && okToDowngradeToNeither)) {
                    result = r;
                    okToDowngradeResultToNeither = okToDowngradeToNeither;
                }
            } else if (result != r) {
                if (okToDowngradeResultToNeither) {
                    if (okToDowngradeToNeither) {
                        result = BetterResult.Neither;
                        okToDowngradeResultToNeither = false;
                        ignoreDowngradableToNeither = true;
                        continue;
                    } else {
                        result = r;
                        okToDowngradeResultToNeither = false;
                        continue;
                    }
                } else if (okToDowngradeToNeither) {
                    continue;
                }

                result = BetterResult.Neither;
                break;
            } else {
                okToDowngradeResultToNeither = okToDowngradeResultToNeither && okToDowngradeToNeither;
            }
        }

        if (result != BetterResult.Neither)
            return result;

        GetParameterCounts(m1, arguments, out var m1ParameterCount, out var m1ParametersUsedIncludingOptional);
        GetParameterCounts(m2, arguments, out var m2ParameterCount, out var m2ParametersUsedIncludingOptional);

        if (allSame && m1ParametersUsedIncludingOptional == m2ParametersUsedIncludingOptional) {
            for (i = i + 1; i < arguments.Count; i++) {
                var argumentKind = arguments[i].expression.kind;

                var type1 = GetParameterTypeAndRefKind(i, m1.result, m1LeastOverriddenParameters, out _);
                var type2 = GetParameterTypeAndRefKind(i, m2.result, m2LeastOverriddenParameters, out _);

                var type1Normalized = type1;
                var type2Normalized = type2;

                // type1Normalized = type1.NormalizeTaskTypes(Compilation);
                // type2Normalized = type2.NormalizeTaskTypes(Compilation);

                if (conversions.ClassifyImplicitConversionFromType(type1Normalized, type2Normalized).kind
                    != ConversionKind.Identity) {
                    allSame = false;
                    break;
                }
            }
        }

        if (!allSame || m1ParametersUsedIncludingOptional != m2ParametersUsedIncludingOptional) {
            if (m1ParametersUsedIncludingOptional != m2ParametersUsedIncludingOptional) {
                // if (m1.result.kind == MemberResolutionKind.ApplicableInExpandedForm) {
                //     if (m2.result.kind != MemberResolutionKind.ApplicableInExpandedForm) {
                //         return BetterResult.Right;
                //     }
                // } else if (m2.Result.Kind == MemberResolutionKind.ApplicableInExpandedForm) {
                //     Debug.Assert(m1.Result.Kind != MemberResolutionKind.ApplicableInExpandedForm);
                //     return BetterResult.Left;
                // }

                if (m1ParametersUsedIncludingOptional == arguments.Count) {
                    return BetterResult.Left;
                } else if (m2ParametersUsedIncludingOptional == arguments.Count) {
                    return BetterResult.Right;
                }
            }

            return PreferValOverInOrRefInterpolatedHandlerParameters(
                arguments,
                m1,
                m1LeastOverriddenParameters,
                m2,
                m2LeastOverriddenParameters
            );
        }

        if (m1.member.GetMemberArity() == 0) {
            if (m2.member.GetMemberArity() > 0)
                return BetterResult.Left;
        } else if (m2.member.GetMemberArity() == 0) {
            return BetterResult.Right;
        }

        var hasAll1 = m1ParameterCount == arguments.Count;
        var hasAll2 = m2ParameterCount == arguments.Count;

        if (hasAll1 && !hasAll2)
            return BetterResult.Left;

        if (!hasAll1 && hasAll2)
            return BetterResult.Right;

        using (var uninst1 = TemporaryArray<TypeSymbol>.Empty)
        using (var uninst2 = TemporaryArray<TypeSymbol>.Empty) {
            var m1DefinitionParameters = m1.leastOverriddenMember.originalDefinition.GetParameters();
            var m2DefinitionParameters = m2.leastOverriddenMember.originalDefinition.GetParameters();

            for (i = 0; i < arguments.Count; i++) {
                uninst1.Add(GetParameterTypeAndRefKind(i, m1.result, m1DefinitionParameters, out _));
                uninst2.Add(GetParameterTypeAndRefKind(i, m2.result, m2DefinitionParameters, out _));
            }

            result = MoreSpecificType(ref uninst1.AsRef(), ref uninst2.AsRef());

            if (result != BetterResult.Neither)
                return result;
        }

        result = PreferValOverInOrRefInterpolatedHandlerParameters(
            arguments,
            m1,
            m1LeastOverriddenParameters,
            m2,
            m2LeastOverriddenParameters
        );

        if (result != BetterResult.Neither)
            return result;

        return BetterResult.Neither;

        static TypeSymbol GetParameterTypeAndRefKind(
            int i,
            MemberAnalysisResult result,
            ImmutableArray<ParameterSymbol> parameters,
            out RefKind parameter1RefKind) {
            var parameter = GetParameter(i, result, parameters);
            parameter1RefKind = parameter.refKind;
            return parameter.type;
        }
    }

    private static BetterResult PreferValOverInOrRefInterpolatedHandlerParameters<TMember>(
        ArrayBuilder<BoundExpressionOrTypeOrConstant> arguments,
        MemberResolutionResult<TMember> m1,
        ImmutableArray<ParameterSymbol> parameters1,
        MemberResolutionResult<TMember> m2,
        ImmutableArray<ParameterSymbol> parameters2)
        where TMember : Symbol {
        var valOverInOrRefInterpolatedHandlerPreference = BetterResult.Neither;

        for (var i = 0; i < arguments.Count; i++) {
            var p1 = GetParameter(i, m1.result, parameters1);
            var p2 = GetParameter(i, m2.result, parameters2);

            if (m1.isValid && m2.isValid) {
                var c1 = m1.result.ConversionForArg(i);
                var c2 = m2.result.ConversionForArg(i);
            }

            if (p1.refKind == RefKind.None && IsAcceptableRefMismatch(p2.refKind)) {
                if (valOverInOrRefInterpolatedHandlerPreference == BetterResult.Right)
                    return BetterResult.Neither;
                else
                    valOverInOrRefInterpolatedHandlerPreference = BetterResult.Left;
            } else if (p2.refKind == RefKind.None && IsAcceptableRefMismatch(p1.refKind)) {
                if (valOverInOrRefInterpolatedHandlerPreference == BetterResult.Left)
                    return BetterResult.Neither;
                else
                    valOverInOrRefInterpolatedHandlerPreference = BetterResult.Right;
            }
        }

        return valOverInOrRefInterpolatedHandlerPreference;

        static bool IsAcceptableRefMismatch(RefKind refKind)
            => refKind switch {
                RefKind.RefConst => true,
                _ => false
            };
    }

    private BetterResult BetterConversionFromExpression(BoundExpression node, TypeSymbol t1, TypeSymbol t2) {
        return BetterConversionFromExpression(
            node,
            t1,
            conversions.ClassifyImplicitConversionFromExpression(node, t1),
            t2,
            conversions.ClassifyImplicitConversionFromExpression(node, t2),
            out _
        );
    }

    private BetterResult BetterConversionFromExpression(
        BoundExpression node,
        TypeSymbol t1,
        Conversion conv1,
        RefKind refKind1,
        TypeSymbol t2,
        Conversion conv2,
        RefKind refKind2,
        bool considerRefKinds,
        out bool okToDowngradeToNeither) {
        okToDowngradeToNeither = false;

        if (considerRefKinds) {
            if (refKind1 != refKind2) {
                if (refKind1 == RefKind.None)
                    return conv1.kind == ConversionKind.Identity ? BetterResult.Left : BetterResult.Neither;
                else
                    return conv2.kind == ConversionKind.Identity ? BetterResult.Right : BetterResult.Neither;
            } else if (refKind1 == RefKind.Ref) {
                return BetterResult.Neither;
            }
        }

        return BetterConversionFromExpression(node, t1, conv1, t2, conv2, out okToDowngradeToNeither);
    }

    private BetterResult BetterConversionFromExpression(
        BoundExpression node,
        TypeSymbol t1,
        Conversion conv1,
        TypeSymbol t2,
        Conversion conv2,
        out bool okToDowngradeToNeither) {
        okToDowngradeToNeither = false;

        if (Conversions.HasIdentityConversion(t1, t2))
            return BetterResult.Neither;

        var nodeKind = node.kind;

        // TODO See other comment about function conversions
        // switch ((conv1.kind, conv2.kind)) {
        //     case (ConversionKind.FunctionType, ConversionKind.FunctionType):
        //         break;
        //     case (_, ConversionKind.FunctionType):
        //         return BetterResult.Left;
        //     case (ConversionKind.FunctionType, _):
        //         return BetterResult.Right;
        // }

        var t1MatchesExactly = ExpressionMatchExactly(node, t1);
        var t2MatchesExactly = ExpressionMatchExactly(node, t2);

        if (t1MatchesExactly) {
            if (!t2MatchesExactly) {
                okToDowngradeToNeither = false;
                return BetterResult.Left;
            }
        } else if (t2MatchesExactly) {
            okToDowngradeToNeither = false;
            return BetterResult.Right;
        }

        // TODO Conditional conversions
        // if (!conv1.IsConditionalExpression && conv2.IsConditionalExpression)
        //     return BetterResult.Left;
        // if (!conv2.IsConditionalExpression && conv1.IsConditionalExpression)
        //     return BetterResult.Right;

        // TODO Collection conversions
        // if (conv1.kind == ConversionKind.CollectionExpression &&
        //     conv2.kind == ConversionKind.CollectionExpression) {
        //     return BetterCollectionExpressionConversion((BoundUnconvertedCollectionExpression)node, t1, conv1, t2, conv2, ref useSiteInfo);
        // }

        return BetterConversionTarget(node, t1, conv1, t2, conv2, out okToDowngradeToNeither);
    }

    private BetterResult BetterConversionTarget(
        BoundNode node,
        TypeSymbol type1,
        Conversion conv1,
        TypeSymbol type2,
        Conversion conv2,
        out bool okToDowngradeToNeither) {
        return BetterConversionTargetCore(
            node,
            type1,
            conv1,
            type2,
            conv2,
            out okToDowngradeToNeither,
            BetterConversionTargetRecursionLimit
        );
    }

    private BetterResult BetterConversionTargetCore(
        BoundNode node,
        TypeSymbol type1,
        Conversion conv1,
        TypeSymbol type2,
        Conversion conv2,
        out bool okToDowngradeToNeither,
        int betterConversionTargetRecursionLimit) {
        okToDowngradeToNeither = false;

        if (Conversions.HasIdentityConversion(type1, type2))
            return BetterResult.Neither;

        var type1ToType2 = Conversion.CollapseConversion(
            conversions.ClassifyImplicitConversionFromType(type1, type2)
        ).isImplicit;

        var type2ToType1 = Conversion.CollapseConversion(
            conversions.ClassifyImplicitConversionFromType(type2, type1)
        ).isImplicit;

        if (type1ToType2) {
            if (type2ToType1) {
                // TODO This boxing check is probably misplaced
                if (conv1.isBoxing && conv2.isBoxing)
                    return BetterResult.Neither;
                else if (conv1.isBoxing)
                    return BetterResult.Right;
                else if (conv2.isBoxing)
                    return BetterResult.Left;
            } else {
                okToDowngradeToNeither = false;
                return BetterResult.Left;
            }
        } else if (type2ToType1) {
            okToDowngradeToNeither = false;
            return BetterResult.Right;
        }

        if (IsSignedIntegralType(type1)) {
            if (IsUnsignedIntegralType(type2))
                return BetterResult.Left;
        } else if (IsUnsignedIntegralType(type1) && IsSignedIntegralType(type2)) {
            return BetterResult.Right;
        }

        return BetterResult.Neither;
    }

    private bool IsSignedIntegralType(TypeSymbol type) {
        if (type is not null && type.IsNullableType())
            type = type.GetNullableUnderlyingType();

        switch (type.GetSpecialTypeSafe()) {
            case SpecialType.Int8:
            case SpecialType.Int16:
            case SpecialType.Int32:
            case SpecialType.Int64:
            case SpecialType.Int:
            case SpecialType.IntPtr:
                return true;
            default:
                return false;
        }
    }

    private static bool IsUnsignedIntegralType(TypeSymbol type) {
        if (type is not null && type.IsNullableType())
            type = type.GetNullableUnderlyingType();

        switch (type.GetSpecialTypeSafe()) {
            case SpecialType.UInt8:
            case SpecialType.UInt16:
            case SpecialType.UInt32:
            case SpecialType.UInt64:
            case SpecialType.UIntPtr:
                return true;
            default:
                return false;
        }
    }

    private bool ExpressionMatchExactly(BoundExpression node, TypeSymbol t) {
        if (node.Type() is not null && Conversions.HasIdentityConversion(node.Type(), t))
            return true;

        return false;
    }

    private static BetterResult MoreSpecificType(ref TemporaryArray<TypeSymbol> t1, ref TemporaryArray<TypeSymbol> t2) {
        var result = BetterResult.Neither;

        for (var i = 0; i < t1.Count; i++) {
            var r = MoreSpecificType(t1[i], t2[i]);

            if (r == BetterResult.Neither) {
            } else if (result == BetterResult.Neither) {
                result = r;
            } else if (result != r) {
                return BetterResult.Neither;
            }
        }

        return result;
    }

    private static BetterResult MoreSpecificType(TypeSymbol t1, TypeSymbol t2) {
        // This should only happen with PE symbols
        if (t1.IsErrorType() && t2.IsErrorType())
            return BetterResult.Neither;

        if (t1.IsErrorType())
            return BetterResult.Right;

        if (t2.IsErrorType())
            return BetterResult.Left;

        var t1IsTypeParameter = t1.IsTemplateParameter();
        var t2IsTypeParameter = t2.IsTemplateParameter();

        if (t1IsTypeParameter && !t2IsTypeParameter)
            return BetterResult.Right;

        if (!t1IsTypeParameter && t2IsTypeParameter)
            return BetterResult.Left;

        if (t1IsTypeParameter && t2IsTypeParameter)
            return BetterResult.Neither;

        if (t1.IsArray()) {
            var arr1 = (ArrayTypeSymbol)t1;
            var arr2 = (ArrayTypeSymbol)t2;

            return MoreSpecificType(arr1.elementType, arr2.elementType);
        }

        var n2 = t2 as NamedTypeSymbol;

        if (t1 is not NamedTypeSymbol n1)
            return BetterResult.Neither;

        using var allTypeArgs1 = TemporaryArray<TypeSymbol>.Empty;
        using var allTypeArgs2 = TemporaryArray<TypeSymbol>.Empty;
        n1.GetAllTypeArguments(ref allTypeArgs1.AsRef());
        n2.GetAllTypeArguments(ref allTypeArgs2.AsRef());

        var result = MoreSpecificType(ref allTypeArgs1.AsRef(), ref allTypeArgs2.AsRef());

        return result;
    }

    private static ParameterSymbol GetParameter(
        int argIndex,
        MemberAnalysisResult result,
        ImmutableArray<ParameterSymbol> parameters) {
        var paramIndex = result.ParameterFromArgument(argIndex);
        return parameters[paramIndex];
    }

    private static bool RequiredFunctionType<TMember>(MemberResolutionResult<TMember> m) where TMember : Symbol {
        if (m.hasTemplateArgumentInferredFromFunctionType)
            return true;

        var conversions = m.result.conversions;

        if (conversions.IsDefault)
            return false;

        // TODO Do we need function type conversions?
        // return conversions.Any(static c => c.kind == ConversionKind.FunctionType);
        return false;
    }

    private static void GetParameterCounts<TMember>(
        MemberResolutionResult<TMember> m,
        ArrayBuilder<BoundExpressionOrTypeOrConstant> arguments,
        out int declaredParameterCount,
        out int parametersUsedIncludingExpansionAndOptional)
        where TMember : Symbol {
        declaredParameterCount = m.member.GetParameterCount();

        // TODO Surely we skip the valid logic because ApplicableInExpandedForm doesn't exist?
        // if (m.result.kind == MemberResolutionKind.ApplicableInExpandedForm) {
        //     if (arguments.Count < declaredParameterCount) {
        //         ImmutableArray<int> argsToParamsOpt = m.Result.ArgsToParamsOpt;

        //         if (argsToParamsOpt.IsDefaultOrEmpty || !argsToParamsOpt.Contains(declaredParameterCount - 1)) {
        //             // params parameter isn't used (see ExpressionBinder::TryGetExpandedParams in the native compiler)
        //             parametersUsedIncludingExpansionAndOptional = declaredParameterCount - 1;
        //         } else {
        //             // params parameter is used by a named argument
        //             parametersUsedIncludingExpansionAndOptional = declaredParameterCount;
        //         }
        //     } else {
        //         parametersUsedIncludingExpansionAndOptional = arguments.Count;
        //     }
        // } else {
        //     parametersUsedIncludingExpansionAndOptional = declaredParameterCount;
        // }
        parametersUsedIncludingExpansionAndOptional = declaredParameterCount;
    }

    private MemberResolutionResult<TMember> IsMemberApplicableInNormalForm<TMember>(
        TMember member,
        TMember leastOverriddenMember,
        ArrayBuilder<TypeOrConstant> templateArguments,
        AnalyzedArguments arguments,
        bool completeResults)
        where TMember : Symbol {
        var argumentAnalysis = AnalyzeArguments(
            member.GetParameters().ToImmutableArray<Symbol>(),
            arguments,
            isMethodGroupConversion: false,
            expanded: false
        );

        if (!argumentAnalysis.isValid) {
            switch (argumentAnalysis.kind) {
                case ArgumentAnalysisResultKind.RequiredParameterMissing:
                case ArgumentAnalysisResultKind.NoCorrespondingParameter:
                case ArgumentAnalysisResultKind.DuplicateNamedArgument:
                    if (!completeResults)
                        goto default;

                    break;
                default:
                    return new MemberResolutionResult<TMember>(
                        member,
                        leastOverriddenMember,
                        MemberAnalysisResult.ArgumentParameterMismatch(argumentAnalysis),
                        hasTypeArgumentInferredFromFunctionType: false
                    );
            }
        }

        var leastOverriddenMemberConstructedFrom = GetConstructedFrom(leastOverriddenMember);

        var constructedFromEffectiveParameters = GetEffectiveParametersInNormalForm(
            leastOverriddenMemberConstructedFrom,
            arguments.arguments.Count,
            argumentAnalysis.argsToParams,
            arguments.refKinds,
            out var hasAnyRefOmittedArgument
        );

        var applicableResult = IsApplicable(
            member,
            leastOverriddenMemberConstructedFrom,
            templateArguments,
            arguments,
            constructedFromEffectiveParameters,
            argumentAnalysis.argsToParams,
            hasAnyRefOmittedArgument: hasAnyRefOmittedArgument,
            completeResults: completeResults
        );

        if (completeResults && !argumentAnalysis.isValid) {
            return new MemberResolutionResult<TMember>(
                member,
                leastOverriddenMember,
                MemberAnalysisResult.ArgumentParameterMismatch(argumentAnalysis),
                hasTypeArgumentInferredFromFunctionType: false
            );
        }

        return applicableResult;
    }

    private static EffectiveParameters GetEffectiveParametersInNormalForm<TMember>(
        TMember member,
        int argumentCount,
        ImmutableArray<int> argToParamMap,
        ArrayBuilder<RefKind> argumentRefKinds,
        out bool hasAnyRefOmittedArgument)
        where TMember : Symbol {
        hasAnyRefOmittedArgument = false;
        var parameters = member.GetParameters();

        var parameterCount = member.GetParameterCount();

        if (argumentCount == parameterCount && argToParamMap.IsDefaultOrEmpty) {
            var parameterRefKinds = member.GetParameterRefKinds();

            if (parameterRefKinds.IsDefaultOrEmpty) {
                return new EffectiveParameters(
                    member.GetParameterTypes(),
                    parameterRefKinds,
                    firstParamsElementIndex: -1
                );
            }
        }

        var types = ArrayBuilder<TypeWithAnnotations>.GetInstance();
        ArrayBuilder<RefKind> refs = null;
        var hasAnyRefArg = argumentRefKinds.Any();

        for (var arg = 0; arg < argumentCount; ++arg) {
            var parm = argToParamMap.IsDefault ? arg : argToParamMap[arg];

            if (parm >= parameters.Length)
                continue;

            var parameter = parameters[parm];
            types.Add(parameter.typeWithAnnotations);

            var argRefKind = hasAnyRefArg ? argumentRefKinds[arg] : RefKind.None;
            var paramRefKind = GetEffectiveParameterRefKind(parameter, argRefKind, ref hasAnyRefOmittedArgument);

            if (refs is null) {
                if (paramRefKind != RefKind.None) {
                    refs = ArrayBuilder<RefKind>.GetInstance(arg, RefKind.None);
                    refs.Add(paramRefKind);
                }
            } else {
                refs.Add(paramRefKind);
            }
        }

        var refKinds = refs is not null ? refs.ToImmutableAndFree() : default;
        return new EffectiveParameters(types.ToImmutableAndFree(), refKinds, firstParamsElementIndex: -1);
    }

    private MemberResolutionResult<TMember> IsApplicable<TMember>(
        TMember member,
        TMember leastOverriddenMember,
        ArrayBuilder<TypeOrConstant> typeArgumentsBuilder,
        AnalyzedArguments arguments,
        EffectiveParameters constructedFromEffectiveParameters,
        ImmutableArray<int> argsToParamsMap,
        bool hasAnyRefOmittedArgument,
        bool completeResults)
        where TMember : Symbol {
        MethodSymbol method;
        EffectiveParameters constructedEffectiveParameters;
        var hasTypeArgumentsInferredFromFunctionType = false;

        if (member.kind == SymbolKind.Method && (method = (MethodSymbol)(Symbol)member).arity > 0) {
            var leastOverriddenMethod = (MethodSymbol)(Symbol)leastOverriddenMember;
            ImmutableArray<TypeOrConstant> typeArguments;

            if (typeArgumentsBuilder.Count > 0) {
                typeArguments = typeArgumentsBuilder.ToImmutable();
            } else {
                typeArguments = InferMethodTypeArguments(
                    method,
                    leastOverriddenMethod.constructedFrom.templateParameters,
                    arguments,
                    constructedFromEffectiveParameters,
                    out hasTypeArgumentsInferredFromFunctionType,
                    out var inferenceError
                );

                if (typeArguments.IsDefault) {
                    return new MemberResolutionResult<TMember>(
                        member,
                        leastOverriddenMember,
                        inferenceError,
                        hasTypeArgumentInferredFromFunctionType: false
                    );
                }
            }

            member = (TMember)(Symbol)method.Construct(typeArguments);
            leastOverriddenMember = (TMember)(Symbol)leastOverriddenMethod.constructedFrom.Construct(typeArguments);

            var parameterTypes = leastOverriddenMember.GetParameterTypes();
            var parameters = leastOverriddenMember.GetParameters();

            for (var i = 0; i < parameterTypes.Length; i++) {
                var _ = BelteDiagnosticQueue.GetInstance();
                parameterTypes[i].type.CheckAllConstraints(parameters[i].location, _);

                if (_.Any()) {
                    _.Free();
                    return new MemberResolutionResult<TMember>(
                        member,
                        leastOverriddenMember,
                        MemberAnalysisResult.ConstructedParameterFailedConstraintsCheck(i),
                        hasTypeArgumentsInferredFromFunctionType
                    );
                }

                _.Free();
            }

            var map = new TemplateMap(leastOverriddenMethod.templateParameters, typeArguments); // ? allowAlpha: true

            constructedEffectiveParameters = new EffectiveParameters(
                map.SubstituteTypes(constructedFromEffectiveParameters.parameterTypes)
                    .Select(t => t.type).ToImmutableArray(),
                constructedFromEffectiveParameters.parameterRefKinds,
                constructedFromEffectiveParameters.firstParamsElementIndex
            );
        } else {
            constructedEffectiveParameters = constructedFromEffectiveParameters;
        }

        var applicableResult = IsApplicable(
            member,
            constructedEffectiveParameters,
            arguments,
            argsToParamsMap,
            hasAnyRefOmittedArgument: hasAnyRefOmittedArgument,
            completeResults: completeResults
        );

        return new MemberResolutionResult<TMember>(
            member,
            leastOverriddenMember,
            applicableResult,
            hasTypeArgumentsInferredFromFunctionType
        );
    }

    private ImmutableArray<TypeOrConstant> InferMethodTypeArguments(
        MethodSymbol method,
        ImmutableArray<TemplateParameterSymbol> originalTemplateParameters,
        AnalyzedArguments arguments,
        EffectiveParameters originalEffectiveParameters,
        out bool hasTypeArgumentsInferredFromFunctionType,
        out MemberAnalysisResult error) {
        // TODO Type inferrer
        // var args = arguments.arguments.ToImmutable();

        // var inferenceResult = MethodTypeInferrer.Infer(
        //     _binder,
        //     _binder.Conversions,
        //     originalTemplateParameters,
        //     method.ContainingType,
        //     originalEffectiveParameters.ParameterTypes,
        //     originalEffectiveParameters.ParameterRefKinds,
        //     args,
        //     ref useSiteInfo);

        // if (inferenceResult.Success) {
        //     hasTypeArgumentsInferredFromFunctionType = inferenceResult.HasTypeArgumentInferredFromFunctionType;
        //     error = default;
        //     return inferenceResult.InferredTypeArguments;
        // }

        hasTypeArgumentsInferredFromFunctionType = false;
        error = MemberAnalysisResult.TypeInferenceFailed();
        return default;
    }

    private MemberAnalysisResult IsApplicable(
        Symbol candidate,
        EffectiveParameters parameters,
        AnalyzedArguments arguments,
        ImmutableArray<int> argsToParameters,
        bool hasAnyRefOmittedArgument,
        bool completeResults) {
        var paramCount = parameters.parameterTypes.Length;

        if (arguments.arguments.Count < paramCount)
            paramCount = arguments.arguments.Count;

        ArrayBuilder<Conversion> conversions = null;
        BitVector badArguments = default;

        for (var argumentPosition = 0; argumentPosition < paramCount; argumentPosition++) {
            var argument = arguments.Argument(argumentPosition);
            Conversion conversion;

            var argumentRefKind = arguments.RefKind(argumentPosition);
            var parameterRefKind = parameters.parameterRefKinds.IsDefault
                ? RefKind.None
                : parameters.parameterRefKinds[argumentPosition];

            conversion = CheckArgumentForApplicability(
                candidate,
                argument.expression,
                argumentRefKind,
                parameters.parameterTypes[argumentPosition].type,
                parameterRefKind
            );

            if (!conversion.exists) {
                if (badArguments.isNull)
                    badArguments = BitVector.Create(argumentPosition + 1);

                badArguments[argumentPosition] = true;
            }

            if (conversions != null) {
                conversions.Add(conversion);
            } else if (!conversion.isIdentity) {
                conversions = ArrayBuilder<Conversion>.GetInstance(paramCount);
                conversions.AddMany(Conversion.Identity, argumentPosition);
                conversions.Add(conversion);
            }

            if (!badArguments.isNull && !completeResults) {
                break;
            }
        }

        MemberAnalysisResult result;
        var conversionsArray = conversions is not null ? conversions.ToImmutableAndFree() : default;

        if (!badArguments.isNull) {
            result = MemberAnalysisResult.BadArgumentConversions(
                argsToParameters,
                badArguments,
                conversionsArray
            );
        } else {
            result = MemberAnalysisResult.Applicable(argsToParameters, conversionsArray, hasAnyRefOmittedArgument);
        }

        return result;
    }

    private Conversion CheckArgumentForApplicability(
        Symbol candidate,
        BoundExpression argument,
        RefKind argRefKind,
        TypeSymbol parameterType,
        RefKind parRefKind) {
        if (argRefKind != parRefKind)
            return Conversion.None;

        var argType = argument.Type();

        if (argRefKind == RefKind.None) {
            argument = Binder.ReduceNumericIfApplicable(parameterType, argument);
            var conversion = conversions.ClassifyImplicitConversionFromExpression(argument, parameterType);
            return conversion;
        }

        if (argType is not null && Conversions.HasIdentityConversion(argType, parameterType, includeNullability: false))
            return Conversion.Identity;
        else
            return Conversion.None;
    }

    private static RefKind GetEffectiveParameterRefKind(
        ParameterSymbol parameter,
        RefKind argRefKind,
        ref bool hasAnyRefOmittedArgument) {
        var paramRefKind = parameter.refKind;

        if (paramRefKind == RefKind.RefConst && argRefKind is RefKind.None or RefKind.Ref)
            return argRefKind;

        // TODO Consider allowing this for COM interop?
        // if (paramRefKind == RefKind.Ref && argRefKind == RefKind.None) {
        //     hasAnyRefOmittedArgument = true;
        //     return RefKind.None;
        // }

        return paramRefKind;
    }

    private static Dictionary<NamedTypeSymbol, ArrayBuilder<T>> PartitionMembersByContainingType<T>(
        ArrayBuilder<T> members) where T : Symbol {
        var containingTypeMap = new Dictionary<NamedTypeSymbol, ArrayBuilder<T>>();

        for (var i = 0; i < members.Count; i++) {
            var member = members[i];
            var containingType = member.containingType;

            if (!containingTypeMap.TryGetValue(containingType, out var builder)) {
                builder = ArrayBuilder<T>.GetInstance();
                containingTypeMap[containingType] = builder;
            }

            builder.Add(member);
        }

        return containingTypeMap;
    }

    internal static ArgumentAnalysisResult AnalyzeArguments(
        ImmutableArray<Symbol> parameters,
        AnalyzedArguments arguments,
        bool isMethodGroupConversion,
        bool expanded) {
        if (!expanded && arguments.names.Count == 0)
            return AnalyzeArgumentsForNormalFormNoNamedArguments(parameters, arguments, isMethodGroupConversion);

        var argumentCount = arguments.arguments.Count;

        int[] parametersPositions = null;
        int? unmatchedArgumentIndex = null;
        bool? unmatchedArgumentIsNamed = null;

        var seenNamedParams = false;
        var seenOutOfPositionNamedArgument = false;

        for (var argumentPosition = 0; argumentPosition < argumentCount; argumentPosition++) {
            var parameterPosition = CorrespondsToAnyParameter(
                parameters,
                expanded,
                arguments,
                argumentPosition,
                out var isNamedArgument,
                ref seenNamedParams,
                ref seenOutOfPositionNamedArgument
            ) ?? -1;

            if (parameterPosition == -1 && unmatchedArgumentIndex is null) {
                unmatchedArgumentIndex = argumentPosition;
                unmatchedArgumentIsNamed = isNamedArgument;
            }

            if (parameterPosition != argumentPosition && parametersPositions is null) {
                parametersPositions = new int[argumentCount];

                for (var i = 0; i < argumentPosition; i++)
                    parametersPositions[i] = i;
            }

            parametersPositions?[argumentPosition] = parameterPosition;
        }

        var argsToParameters = new ParameterMap(parametersPositions, argumentCount);

        var badNonTrailingNamedArgument = CheckForBadNonTrailingNamedArgument(arguments, argsToParameters);

        if (badNonTrailingNamedArgument is not null)
            return ArgumentAnalysisResult.BadNonTrailingNamedArgument(badNonTrailingNamedArgument.Value);

        if (unmatchedArgumentIndex is not null) {
            if (unmatchedArgumentIsNamed.Value)
                return ArgumentAnalysisResult.NoCorrespondingNamedParameter(unmatchedArgumentIndex.Value);
            else
                return ArgumentAnalysisResult.NoCorrespondingParameter(unmatchedArgumentIndex.Value);
        }

        var nameUsedForPositional = NameUsedForPositional(arguments, argsToParameters);

        if (nameUsedForPositional is not null)
            return ArgumentAnalysisResult.NameUsedForPositional(nameUsedForPositional.Value);

        var requiredParameterMissing = CheckForMissingRequiredParameter(
            argsToParameters,
            parameters,
            isMethodGroupConversion,
            expanded
        );

        if (requiredParameterMissing is not null)
            return ArgumentAnalysisResult.RequiredParameterMissing(requiredParameterMissing.Value);

        var duplicateNamedArgument = CheckForDuplicateNamedArgument(arguments);

        if (duplicateNamedArgument is not null)
            return ArgumentAnalysisResult.DuplicateNamedArgument(duplicateNamedArgument.Value);

        return ArgumentAnalysisResult.NormalForm(argsToParameters.ToImmutableArray());
    }

    private static int? CheckForDuplicateNamedArgument(AnalyzedArguments arguments) {
        if (arguments.names.Count == 0)
            return null;

        var alreadyDefined = PooledHashSet<string>.GetInstance();

        for (var i = 0; i < arguments.names.Count; i++) {
            var name = arguments.Name(i);

            if (name is null)
                continue;

            if (!alreadyDefined.Add(name)) {
                alreadyDefined.Free();
                return i;
            }
        }

        alreadyDefined.Free();
        return null;
    }

    private static int? CheckForBadNonTrailingNamedArgument(
        AnalyzedArguments arguments,
        ParameterMap argsToParameters) {
        if (argsToParameters.isTrivial)
            return null;

        var foundPosition = -1;
        var length = arguments.arguments.Count;

        for (var i = 0; i < length; i++) {
            var parameter = argsToParameters[i];

            if (parameter != -1 && parameter != i && arguments.Name(i) is not null) {
                foundPosition = i;
                break;
            }
        }

        if (foundPosition != -1) {
            for (var i = foundPosition + 1; i < length; i++) {
                if (arguments.Name(i) is null)
                    return foundPosition;
            }
        }

        return null;
    }

    private static int? CheckForMissingRequiredParameter(
        ParameterMap argsToParameters,
        ImmutableArray<Symbol> parameters,
        bool isMethodGroupConversion,
        bool expanded) {
        var count = expanded ? parameters.Length - 1 : parameters.Length;

        if (argsToParameters.isTrivial && count <= argsToParameters.length)
            return null;

        for (var p = 0; p < count; p++) {
            if (CanBeOptional(parameters[p], isMethodGroupConversion))
                continue;

            var found = false;

            for (var arg = 0; arg < argsToParameters.length; ++arg) {
                found = argsToParameters[arg] == p;

                if (found)
                    break;
            }

            if (!found)
                return p;
        }

        return null;
    }

    private static int? NameUsedForPositional(
        AnalyzedArguments arguments,
        ParameterMap argsToParameters) {
        if (argsToParameters.isTrivial)
            return null;

        // TODO, Forwarded
        // No chance this ever exceeds negligibility right?
        // PERFORMANCE: This is an O(n-squared) algorithm, but n will typically be small.  We could rewrite this
        // PERFORMANCE: as a linear algorithm if we wanted to allocate more memory.

        for (var argumentPosition = 0; argumentPosition < argsToParameters.length; argumentPosition++) {
            if (arguments.Name(argumentPosition) is not null) {
                for (var i = 0; i < argumentPosition; i++) {
                    if (arguments.Name(i) is null) {
                        if (argsToParameters[argumentPosition] == argsToParameters[i])
                            return argumentPosition;
                    }
                }
            }
        }

        return null;
    }

    private static int? CorrespondsToAnyParameter(
        ImmutableArray<Symbol> memberParameters,
        bool expanded,
        AnalyzedArguments arguments,
        int argumentPosition,
        out bool isNamedArgument,
        ref bool seenNamedParams,
        ref bool seenOutOfPositionNamedArgument) {
        isNamedArgument = arguments.names.Count > argumentPosition && arguments.names[argumentPosition] is not null;

        if (!isNamedArgument) {
            if (seenNamedParams)
                return null;

            if (seenOutOfPositionNamedArgument)
                return null;

            var parameterCount = memberParameters.Length;

            if (argumentPosition >= parameterCount)
                return expanded ? parameterCount - 1 : null;

            return argumentPosition;
        } else {
            var name = arguments.names[argumentPosition].GetValueOrDefault().Name;

            for (var p = 0; p < memberParameters.Length; p++) {
                if (memberParameters[p].name == name) {
                    if (expanded && p == memberParameters.Length - 1)
                        seenNamedParams = true;

                    if (p != argumentPosition)
                        seenOutOfPositionNamedArgument = true;

                    return p;
                }
            }
        }

        return null;
    }

    private static ArgumentAnalysisResult AnalyzeArgumentsForNormalFormNoNamedArguments(
        ImmutableArray<Symbol> parameters,
        AnalyzedArguments arguments,
        bool isMethodGroupConversion) {
        var parameterCount = parameters.Length;
        var argumentCount = arguments.arguments.Count;

        if (argumentCount < parameterCount) {
            for (var parameterPosition = argumentCount; parameterPosition < parameterCount; parameterPosition++) {
                if (parameters.Length == parameterPosition ||
                    !CanBeOptional(parameters[parameterPosition], isMethodGroupConversion)) {
                    return ArgumentAnalysisResult.RequiredParameterMissing(parameterPosition);
                }
            }
        } else if (parameterCount < argumentCount) {
            return ArgumentAnalysisResult.NoCorrespondingParameter(parameterCount);
        }

        return ArgumentAnalysisResult.NormalForm(default);
    }

    private static bool CanBeOptional(Symbol parameter, bool isMethodGroupConversion) {
        return !isMethodGroupConversion && parameter.IsOptional();
    }

    private static bool MemberGroupContainsMoreDerivedOverride<TMember>(
        ArrayBuilder<TMember> members,
        TMember member,
        bool checkOverrideContainingType)
        where TMember : Symbol {
        if (!member.isVirtual && !member.isAbstract && !member.isOverride)
            return false;

        if (!member.containingType.IsClassType())
            return false;

        for (var i = 0; i < members.Count; i++) {
            if (IsMoreDerivedOverride(
                member: member,
                moreDerivedOverride: members[i],
                checkOverrideContainingType: checkOverrideContainingType)) {
                return true;
            }
        }

        return false;
    }

    private static bool MemberGroupHidesByName<TMember>(ArrayBuilder<TMember> members, TMember member)
        where TMember : Symbol {
        var memberContainingType = member.containingType;

        foreach (var otherMember in members) {
            var otherContainingType = otherMember.containingType;

            if (HidesByName(otherMember) &&
                otherContainingType.IsDerivedFrom(memberContainingType, TypeCompareKind.ConsiderEverything)) {
                return true;
            }
        }

        return false;
    }

    private static bool IsMoreDerivedOverride(
        Symbol member,
        Symbol moreDerivedOverride,
        bool checkOverrideContainingType) {
        if (!moreDerivedOverride.isOverride ||
            checkOverrideContainingType &&
            !moreDerivedOverride.containingType.IsDerivedFrom(
                member.containingType,
                TypeCompareKind.ConsiderEverything
            ) ||
            !MemberSignatureComparer.SloppyOverrideComparer.Equals(member, moreDerivedOverride)) {
            return false;
        }

        return moreDerivedOverride.GetLeastOverriddenMember(null).originalDefinition ==
            member.GetLeastOverriddenMember(null).originalDefinition;
    }

    private static bool HidesByName(Symbol member) {
        return member.kind switch {
            SymbolKind.Method => ((MethodSymbol)member).hidesBaseMethodsByName,
            _ => throw ExceptionUtilities.UnexpectedValue(member.kind),
        };
    }

    private static TMember GetConstructedFrom<TMember>(TMember member) where TMember : Symbol {
        return member.kind switch {
            SymbolKind.Method => (TMember)(Symbol)(member as MethodSymbol).constructedFrom,
            _ => throw ExceptionUtilities.UnexpectedValue(member.kind),
        };
    }

    private static bool AnyValidResult<TMember>(ArrayBuilder<MemberResolutionResult<TMember>> results)
        where TMember : Symbol {
        foreach (var result in results) {
            if (result.isValid)
                return true;
        }

        return false;
    }

    private static bool SingleValidResult<TMember>(ArrayBuilder<MemberResolutionResult<TMember>> results)
        where TMember : Symbol {
        var oneValid = false;
        foreach (var result in results) {
            if (result.isValid) {
                if (oneValid)
                    return false;

                oneValid = true;
            }
        }

        return oneValid;
    }
}
