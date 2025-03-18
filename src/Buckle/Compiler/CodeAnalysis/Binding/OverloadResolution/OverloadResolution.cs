using System.Collections.Generic;
using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Libraries;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// Resolves overloads to find the best overload.
/// </summary>
internal sealed partial class OverloadResolution {
    private readonly Binder _binder;

    /// <summary>
    /// Creates an <see cref="OverloadResolution" />, uses a Binders diagnostics.
    /// </summary>
    /// <param name="binder"><see cref="Binder" /> to use diagnostics from.</param>
    internal OverloadResolution(Binder binder) {
        _binder = binder;
    }

    internal Conversions conversions => _binder.conversions;

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
            var operators = ArrayBuilder<BinaryOperatorSignature>.GetInstance();
            CorLibrary.GetAllBuiltInBinaryOperators(kind, operators);
            CandidateOperators(operators, left, right, result.results);
            operators.Free();
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
            var operators = ArrayBuilder<UnaryOperatorSignature>.GetInstance();
            CorLibrary.GetAllBuiltInUnaryOperators(kind, operators);
            CandidateOperators(operators, operand, result.results);
            operators.Free();
        }
    }

    internal void MethodOverloadResolution<T>(
        ArrayBuilder<T> members,
        ArrayBuilder<TypeOrConstant> templateArguments,
        BoundExpression receiver,
        AnalyzedArguments arguments,
        OverloadResolutionResult<T> result,
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

    private void PerformObjectCreationOverloadResolution(
        ArrayBuilder<MemberResolutionResult<MethodSymbol>> results,
        ImmutableArray<MethodSymbol> constructors,
        AnalyzedArguments arguments,
        bool completeResults) {
        foreach (var constructor in constructors)
            AddConstructorToCandidateSet(constructor, results, arguments, completeResults);

        // TODO
        // if (!dynamicResolution) {
        //     if (!isEarlyAttributeBinding) {
        //         // If we're still decoding early attributes, we can get into a cycle here where we attempt to decode early attributes,
        //         // which causes overload resolution, which causes us to attempt to decode early attributes, etc. Concretely, this means
        //         // that OverloadResolutionPriorityAttribute won't affect early bound attributes, so you can't use OverloadResolutionPriorityAttribute
        //         // to adjust what constructor of OverloadResolutionPriorityAttribute is chosen. See `CycleOnOverloadResolutionPriorityConstructor_02` for
        //         // an example.
        //         RemoveLowerPriorityMembers<MemberResolutionResult<MethodSymbol>, MethodSymbol>(results);
        //     }

        //     // The best method of the set of candidate methods is identified. If a single best
        //     // method cannot be identified, the method invocation is ambiguous, and a binding-time
        //     // error occurs.
        //     RemoveWorseMembers(results, arguments);
        // }

        return;
    }

    private void AddConstructorToCandidateSet(
        MethodSymbol constructor,
        ArrayBuilder<MemberResolutionResult<MethodSymbol>> results,
        AnalyzedArguments arguments,
        bool completeResults) {
        // TODO
        // var normalResult = IsConstructorApplicableInNormalForm(constructor, arguments, completeResults, ref useSiteInfo);
        // var result = normalResult;
        // if (!normalResult.IsValid) {
        //     if (IsValidParams(_binder, constructor, disallowExpandedNonArrayParams: false, out TypeWithAnnotations definitionElementType)) {
        //         var expandedResult = IsConstructorApplicableInExpandedForm(constructor, arguments, definitionElementType, completeResults, ref useSiteInfo);
        //         if (expandedResult.IsValid || completeResults) {
        //             result = expandedResult;
        //         }
        //     }
        // }

        // // If the constructor has a use site diagnostic, we don't want to discard it because we'll have to report the diagnostic later.
        // if (result.IsValid || completeResults || result.HasUseSiteDiagnosticToReportFor(constructor)) {
        //     results.Add(new MemberResolutionResult<MethodSymbol>(constructor, constructor, result, hasTypeArgumentInferredFromFunctionType: false));
        // }
        var result = MemberAnalysisResult.Applicable([], [], false);
        results.Add(new MemberResolutionResult<MethodSymbol>(constructor, constructor, result, false));
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

            if (convLeft.isImplicit && convRight.isImplicit) {
                results.Add(BinaryOperatorAnalysisResult.Applicable(op, convLeft, convRight));
                hadApplicableCandidate = true;
            } else {
                results.Add(BinaryOperatorAnalysisResult.Inapplicable(op, convLeft, convRight));
            }
        }

        return hadApplicableCandidate;
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
        RefKind returnRefKind,
        TypeSymbol returnType,
        bool checkOverriddenOrHidden)
        where T : Symbol {
        // TODO The following is bare bones to compile, just takes the first available method
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

        TempTakeFirstValid(results);

        void TempTakeFirstValid(ArrayBuilder<MemberResolutionResult<T>> results) {
            var seenValid = false;

            for (var i = 0; i < results.Count; i++) {
                var result = results[i];

                if (result.isValid) {
                    if (seenValid)
                        // ! The actually result here is arbitrary (not used)
                        results[i] = result.WithResult(MemberAnalysisResult.LessDerived());
                    else
                        seenValid = true;
                }
            }
        }
    }

    private void AddMemberToCandidateSet<T>(
        T member,
        ArrayBuilder<MemberResolutionResult<T>> results,
        ArrayBuilder<T> members,
        ArrayBuilder<TypeOrConstant> templateArguments,
        AnalyzedArguments arguments,
        bool completeResults,
        Dictionary<NamedTypeSymbol, ArrayBuilder<T>> containingTypeMap,
        bool checkOverriddenOrHidden = true)
        where T : Symbol {
        // TODO This method is bare bones to compile (no error checking)
        var leastOverriddenMember = (T)member.GetLeastOverriddenMember(_binder.containingType);
        results.Add(new MemberResolutionResult<T>(member, leastOverriddenMember, MemberAnalysisResult.Applicable([], [], false), false));
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
