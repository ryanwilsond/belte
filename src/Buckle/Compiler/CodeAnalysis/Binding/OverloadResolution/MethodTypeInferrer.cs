using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Libraries;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Binding;

internal partial struct MethodTypeInferrer {
    private readonly Compilation _compilation;
    private readonly ConversionsBase _conversions;
    private readonly ImmutableArray<TemplateParameterSymbol> _methodTypeParameters;
    private readonly NamedTypeSymbol _constructedContainingTypeOfMethod;
    private readonly ImmutableArray<TypeWithAnnotations> _formalParameterTypes;
    private readonly ImmutableArray<RefKind> _formalParameterRefKinds;
    private readonly ImmutableArray<BoundExpressionOrTypeOrConstant> _arguments;
    private readonly Extensions _extensions;

    private readonly Dictionary<TemplateParameterSymbol, int> _ordinals;

    private readonly (TypeOrConstant Type, bool FromFunctionType)[] _fixedResults;
    private readonly HashSet<TypeOrConstant>[] _exactBounds;
    private readonly HashSet<TypeOrConstant>[] _upperBounds;
    private readonly HashSet<TypeOrConstant>[] _lowerBounds;

    private Dependency[,] _dependencies;
    private bool _dependenciesDirty;

    private readonly int NumberArgumentsToProcess => System.Math.Min(_arguments.Length, _formalParameterTypes.Length);

    private MethodTypeInferrer(
        Compilation compilation,
        ConversionsBase conversions,
        ImmutableArray<TemplateParameterSymbol> methodTemplateParameters,
        NamedTypeSymbol constructedContainingTypeOfMethod,
        ImmutableArray<TypeWithAnnotations> formalParameterTypes,
        ImmutableArray<RefKind> formalParameterRefKinds,
        ImmutableArray<BoundExpressionOrTypeOrConstant> arguments,
        Extensions extensions,
        Dictionary<TemplateParameterSymbol, int>? ordinals) {
        _compilation = compilation;
        _conversions = conversions;
        _methodTypeParameters = methodTemplateParameters;
        _constructedContainingTypeOfMethod = constructedContainingTypeOfMethod;
        _formalParameterTypes = formalParameterTypes;
        _formalParameterRefKinds = formalParameterRefKinds;
        _arguments = arguments;
        _extensions = extensions ?? Extensions.Default;

        Debug.Assert(ordinals is null || ordinals.Values.Count() == ordinals.Values.Distinct().Count());
        Debug.Assert(ordinals is null || methodTemplateParameters.All(tp => ordinals.ContainsKey(tp)));

        _ordinals = ordinals;
        _fixedResults = new (TypeOrConstant, bool)[methodTemplateParameters.Length];
        _exactBounds = new HashSet<TypeOrConstant>[methodTemplateParameters.Length];
        _upperBounds = new HashSet<TypeOrConstant>[methodTemplateParameters.Length];
        _lowerBounds = new HashSet<TypeOrConstant>[methodTemplateParameters.Length];

        _dependencies = null;
        _dependenciesDirty = false;
    }

    public static MethodTypeInferenceResult Infer(
        Binder binder,
        ConversionsBase conversions,
        ImmutableArray<TemplateParameterSymbol> methodTypeParameters,
        NamedTypeSymbol constructedContainingTypeOfMethod,
        ImmutableArray<TypeWithAnnotations> formalParameterTypes,
        ImmutableArray<RefKind> formalParameterRefKinds,
        ImmutableArray<BoundExpressionOrTypeOrConstant> arguments,
        Dictionary<TemplateParameterSymbol, int> ordinals = null) {
        if (formalParameterTypes.Length == 0) {
            Debug.Assert(methodTypeParameters.Length > 0);
            // Shortcut check if all template arguments have default values
            var builder = ArrayBuilder<TypeOrConstant>.GetInstance();
            var failed = false;

            foreach (var templateParameter in methodTypeParameters) {
                if (templateParameter.defaultValue is null) {
                    failed = true;
                    break;
                }

                builder.Add(templateParameter.defaultValue);
            }

            if (failed) {
                return new MethodTypeInferenceResult(
                    success: false,
                    inferredTypeArguments: default,
                    hasTypeArgumentInferredFromFunctionType: false
                );
            } else {
                return new MethodTypeInferenceResult(
                    success: true,
                    inferredTypeArguments: builder.ToImmutableAndFree(),
                    hasTypeArgumentInferredFromFunctionType: false
                );
            }
        }

        var inferrer = new MethodTypeInferrer(
            binder.compilation,
            conversions,
            methodTypeParameters,
            constructedContainingTypeOfMethod,
            formalParameterTypes,
            formalParameterRefKinds,
            arguments,
            extensions: null,
            ordinals
        );

        return inferrer.InferTemplateArgs(binder);
    }

    private MethodTypeInferenceResult InferTemplateArgs(Binder binder) {
        InferTemplateArgsFirstPhase(binder);
        var success = InferTemplateArgsSecondPhase(binder);
        var inferredTemplateArguments = GetResults(out var inferredFromFunctionType);
        return new MethodTypeInferenceResult(success, inferredTemplateArguments, inferredFromFunctionType);
    }

    private void InferTemplateArgsFirstPhase(Binder binder) {
        Debug.Assert(!_formalParameterTypes.IsDefault);
        Debug.Assert(!_arguments.IsDefault);

        for (int arg = 0, length = NumberArgumentsToProcess; arg < length; arg++) {
            var argument = _arguments[arg];
            var target = _formalParameterTypes[arg];
            var kind = GetRefKind(arg).IsManagedReference() || target.type.IsPointerType()
                ? ExactOrBoundsKind.Exact
                : ExactOrBoundsKind.LowerBound;

            MakeExplicitParameterTypeInferences(binder, argument, target, kind);
        }
    }

    private RefKind GetRefKind(int index) {
        Debug.Assert(0 <= index && index < _formalParameterTypes.Length);
        return _formalParameterRefKinds.IsDefault ? RefKind.None : _formalParameterRefKinds[index];
    }

    private void MakeExplicitParameterTypeInferences(
        Binder binder,
        BoundExpressionOrTypeOrConstant argument,
        TypeWithAnnotations target,
        ExactOrBoundsKind kind) {
        if (argument.isExpression) {
            MakeExplicitParameterTypeInferences(binder, argument.expression, target, kind);
        } else {
            // TODO
            // if (argument.typeOrConstant.isType) {
            // } else {
            // }
        }
    }

    private void MakeExplicitParameterTypeInferences(
        Binder binder,
        BoundExpression argument,
        TypeWithAnnotations target,
        ExactOrBoundsKind kind) {
        // if (argument.kind == BoundKind.UnboundLambda && target.type.GetDelegateType() is { }) {
        //     ExplicitParameterTypeInference(argument, target, ref useSiteInfo);
        //     ExplicitReturnTypeInference(argument, target, ref useSiteInfo);
        if (argument.kind == BoundKind.UnconvertedInitializerList) {
            MakeCollectionExpressionTypeInferences(binder, (BoundUnconvertedInitializerList)argument, target, kind);
        } else if (argument.kind != BoundKind.TupleLiteral ||
            !MakeExplicitParameterTypeInferences(binder, (BoundTupleLiteral)argument, target, kind)) {
            var argumentType = _extensions.GetTypeWithAnnotations(argument);
            if (IsReallyAType(argumentType.type)) {
                ExactOrBoundsInference(kind, argumentType, target);
            }
            // } else if (IsUnfixedTypeParameter(target) && !target.nullableAnnotation.IsAnnotated() && kind is ExactOrBoundsKind.LowerBound) {
            //     var ordinal = GetOrdinal((TypeParameterSymbol)target.Type);
            //     _nullableAnnotationLowerBounds[ordinal] = _nullableAnnotationLowerBounds[ordinal].Join(argumentType.NullableAnnotation);
            // }
        }
    }

    private void ExactOrBoundsInference(ExactOrBoundsKind kind, TypeOrConstant source, TypeOrConstant target) {
        if (source.isType && target.isType)
            ExactOrBoundsInference(kind, source.type, target.type);

        // TODO isConstant
    }

    private void ExactOrBoundsInference(
        ExactOrBoundsKind kind,
        TypeWithAnnotations source,
        TypeWithAnnotations target) {
        switch (kind) {
            case ExactOrBoundsKind.Exact:
                ExactInference(source, target);
                break;
            case ExactOrBoundsKind.LowerBound:
                LowerBoundInference(source, target);
                break;
            case ExactOrBoundsKind.UpperBound:
                UpperBoundInference(source, target);
                break;
        }
    }

    private static bool IsReallyAType(TypeSymbol type) {
        return type is { } &&
            !type.IsErrorType() &&
            !type.IsVoidType();
    }

    private bool MakeExplicitParameterTypeInferences(
        Binder binder,
        BoundTupleLiteral argument,
        TypeWithAnnotations target,
        ExactOrBoundsKind kind) {
        if (target.type.kind != SymbolKind.NamedType)
            return false;

        var destination = (NamedTypeSymbol)target.type;
        var sourceArguments = argument.arguments;

        if (!destination.IsTupleTypeOfCardinality(sourceArguments.Length))
            return false;

        var destTypes = destination.tupleElementTypes;
        Debug.Assert(sourceArguments.Length == destTypes.Length);

        for (var i = 0; i < sourceArguments.Length; i++) {
            var sourceArgument = sourceArguments[i];
            var destType = destTypes[i].type;
            MakeExplicitParameterTypeInferences(binder, sourceArgument, destType, kind);
        }

        return true;
    }

    private void MakeCollectionExpressionTypeInferences(
        Binder binder,
        BoundUnconvertedInitializerList argument,
        TypeWithAnnotations target,
        ExactOrBoundsKind kind) {
        var targetType = target.type;
        Debug.Assert(targetType is { });

        if (targetType is null)
            return;

        if (argument.items.Length == 0)
            return;

        if (!binder.TryGetCollectionIterationType(
            (ExpressionSyntax)argument.syntax,
            targetType.StrippedType(),
            out var targetElementType)) {
            return;
        }

        foreach (var element in argument.items)
            MakeExplicitParameterTypeInferences(binder, element, targetElementType, kind);
    }

    private void ExactInference(TypeOrConstant source, TypeOrConstant target) {
        if (source.isType && target.isType)
            ExactInference(source.type, target.type);

        // TODO
    }

    private void ExactInference(TypeWithAnnotations source, TypeWithAnnotations target) {
        Debug.Assert(source.HasType());
        Debug.Assert(target.HasType());

        if (ExactNullableInference(source, target))
            return;

        if (ExactTemplateParameterInference(source, target))
            return;

        if (ExactArrayInference(source, target))
            return;

        // if (ExactSpanInference(source.type, target.type))
        //     return;

        if (ExactConstructedInference(source, target))
            return;

        if (ExactPointerInference(source, target))
            return;
    }

    private bool ExactNullableInference(TypeWithAnnotations source, TypeWithAnnotations target) {
        return ExactOrBoundsNullableInference(ExactOrBoundsKind.Exact, source, target);
    }

    private bool ExactTemplateParameterInference(TypeWithAnnotations source, TypeWithAnnotations target) {
        Debug.Assert(source.HasType());
        Debug.Assert(target.HasType());

        if (IsUnfixedTemplateParameter(target)) {
            AddBound(source, _exactBounds, target);
            return true;
        }

        return false;
    }

    private void AddBound(
        TypeWithAnnotations addedBound,
        HashSet<TypeOrConstant>[] collectedBounds,
        TypeWithAnnotations methodTypeParameterWithAnnotations) {
        Debug.Assert(IsUnfixedTemplateParameter(methodTypeParameterWithAnnotations));

        var methodTypeParameter = (TemplateParameterSymbol)methodTypeParameterWithAnnotations.type;
        var methodTypeParameterIndex = GetOrdinal(methodTypeParameter);

        if (collectedBounds[methodTypeParameterIndex] is null) {
            collectedBounds[methodTypeParameterIndex] = new HashSet<TypeOrConstant>(
                TypeOrConstant.EqualsComparer.ConsiderEverythingComparer
            );
        }

        collectedBounds[methodTypeParameterIndex].Add(new TypeOrConstant(addedBound));
    }

    private bool ExactOrBoundsNullableInference(
        ExactOrBoundsKind kind,
        TypeWithAnnotations source,
        TypeWithAnnotations target) {
        Debug.Assert(source.HasType());
        Debug.Assert(target.HasType());

        if (source.IsNullableType() && target.IsNullableType()) {
            ExactOrBoundsInference(
                kind,
                ((NamedTypeSymbol)source.type).templateArguments[0],
                ((NamedTypeSymbol)target.type).templateArguments[0]
            );

            return true;
        }

        // if (isNullableOnly(source) && isNullableOnly(target)) {
        //     ExactOrBoundsInference(kind, source.AsNotNullableReferenceType(), target.AsNotNullableReferenceType(), ref useSiteInfo);
        //     return true;
        // }

        return false;

        // True if the type is nullable.
        // static bool isNullableOnly(TypeWithAnnotations type)
        //     => type.NullableAnnotation.IsAnnotated();
    }

    private bool IsUnfixedTemplateParameter(TypeWithAnnotations type) {
        Debug.Assert(type.HasType());

        if (type.typeKind != TypeKind.TemplateParameter)
            return false;

        var typeParameter = (TemplateParameterSymbol)type.type;
        var ordinal = GetOrdinal(typeParameter);

        return ValidIndex(ordinal) &&
            TypeSymbol.Equals(typeParameter, _methodTypeParameters[ordinal], TypeCompareKind.ConsiderEverything) &&
            IsUnfixed(ordinal);
    }

    private int GetOrdinal(TemplateParameterSymbol typeParameter) {
        if (_ordinals is not null)
            return _ordinals[typeParameter];

        return typeParameter.ordinal;
    }

    private bool AllFixed() {
        for (var methodTypeParameterIndex = 0; methodTypeParameterIndex < _methodTypeParameters.Length; methodTypeParameterIndex++) {
            if (IsUnfixed(methodTypeParameterIndex))
                return false;
        }

        return true;
    }

    private bool ValidIndex(int index) {
        return 0 <= index && index < _methodTypeParameters.Length;
    }

    private bool IsUnfixed(int methodTypeParameterIndex) {
        Debug.Assert(ValidIndex(methodTypeParameterIndex));
        return !_fixedResults[methodTypeParameterIndex].Type.HasTypeOrConstant();
    }

    private bool ExactArrayInference(TypeWithAnnotations source, TypeWithAnnotations target) {
        Debug.Assert(source.HasType());
        Debug.Assert(target.HasType());

        if (!source.type.IsArray() || !target.type.IsArray())
            return false;

        var arraySource = (ArrayTypeSymbol)source.type;
        var arrayTarget = (ArrayTypeSymbol)target.type;

        if (!arraySource.HasSameShapeAs(arrayTarget))
            return false;

        ExactInference(arraySource.elementTypeWithAnnotations, arrayTarget.elementTypeWithAnnotations);
        return true;
    }

    private bool ExactConstructedInference(
        TypeWithAnnotations source,
        TypeWithAnnotations target) {
        Debug.Assert(source.HasType());
        Debug.Assert(target.HasType());

        if (source.type is not NamedTypeSymbol namedSource)
            return false;

        if (target.type is not NamedTypeSymbol namedTarget)
            return false;

        if (!TypeSymbol.Equals(
            namedSource.originalDefinition,
            namedTarget.originalDefinition,
            TypeCompareKind.ConsiderEverything)) {
            return false;
        }

        ExactTemplateArgumentInference(namedSource, namedTarget);
        return true;
    }

    private bool ExactPointerInference(TypeWithAnnotations source, TypeWithAnnotations target) {
        if (source.typeKind == TypeKind.Pointer && target.typeKind == TypeKind.Pointer) {

            throw ExceptionUtilities.Unreachable();
            // ExactInference(((PointerTypeSymbol)source.Type).PointedAtTypeWithAnnotations, ((PointerTypeSymbol)target.Type).PointedAtTypeWithAnnotations, ref useSiteInfo);
            // return true;
        } else if (source.type is FunctionPointerTypeSymbol { signature: { parameterCount: int sourceParameterCount } sourceSignature } &&
                   target.type is FunctionPointerTypeSymbol { signature: { parameterCount: int targetParameterCount } targetSignature } &&
                   sourceParameterCount == targetParameterCount) {

            throw ExceptionUtilities.Unreachable();
            // if (!FunctionPointerRefKindsEqual(sourceSignature, targetSignature) || !FunctionPointerCallingConventionsEqual(sourceSignature, targetSignature)) {
            //     return false;
            // }

            // for (int i = 0; i < sourceParameterCount; i++) {
            //     ExactInference(sourceSignature.ParameterTypesWithAnnotations[i], targetSignature.ParameterTypesWithAnnotations[i], ref useSiteInfo);
            // }

            // ExactInference(sourceSignature.ReturnTypeWithAnnotations, targetSignature.ReturnTypeWithAnnotations, ref useSiteInfo);
            // return true;
        }

        return false;
    }

    private void ExactTemplateArgumentInference(NamedTypeSymbol source, NamedTypeSymbol target) {
        Debug.Assert(source is not null);
        Debug.Assert(target is not null);
        Debug.Assert(TypeSymbol.Equals(source.originalDefinition, target.originalDefinition, TypeCompareKind.ConsiderEverything));

        var sourceTypeArguments = ArrayBuilder<TypeOrConstant>.GetInstance();
        var targetTypeArguments = ArrayBuilder<TypeOrConstant>.GetInstance();

        source.GetAllTemplateArguments(sourceTypeArguments);
        target.GetAllTemplateArguments(targetTypeArguments);

        Debug.Assert(sourceTypeArguments.Count == targetTypeArguments.Count);

        for (var arg = 0; arg < sourceTypeArguments.Count; arg++)
            ExactInference(sourceTypeArguments[arg], targetTypeArguments[arg]);

        sourceTypeArguments.Free();
        targetTypeArguments.Free();
    }

    private void LowerBoundInference(TypeOrConstant source, TypeOrConstant target) {
        if (source.isType && target.isType)
            LowerBoundInference(source.type, target.type);

        // TODO
    }

    private void LowerBoundInference(TypeWithAnnotations source, TypeWithAnnotations target) {
        Debug.Assert(source.HasType());
        Debug.Assert(target.HasType());

        if (LowerBoundNullableInference(source, target))
            return;

        if (LowerBoundTemplateParameterInference(source, target))
            return;

        if (LowerBoundArrayInference(source.type, target.type))
            return;

        // if (LowerBoundSpanInference(source.type, target.type))
        //     return;

        // if (LowerBoundNullableInference(pSource, pDest)) {
        //     return;
        // }

        if (LowerBoundTupleInference(source, target))
            return;

        if (LowerBoundConstructedInference(source.type, target.type))
            return;

        if (LowerBoundFunctionPointerTypeInference(source.type, target.type))
            return;
    }

    private bool LowerBoundNullableInference(TypeWithAnnotations source, TypeWithAnnotations target) {
        return ExactOrBoundsNullableInference(ExactOrBoundsKind.LowerBound, source, target);
    }

    private bool LowerBoundTemplateParameterInference(TypeWithAnnotations source, TypeWithAnnotations target) {
        Debug.Assert(source.HasType());
        Debug.Assert(target.HasType());

        if (IsUnfixedTemplateParameter(target)) {
            AddBound(source, _lowerBounds, target);
            return true;
        }

        return false;
    }

    private bool LowerBoundArrayInference(TypeSymbol source, TypeSymbol target) {
        Debug.Assert(source is not null);
        Debug.Assert(target is not null);

        if (!source.IsArray())
            return false;

        var arraySource = (ArrayTypeSymbol)source;
        var elementSource = arraySource.elementTypeWithAnnotations;
        var elementTarget = GetMatchingElementType(arraySource, target);

        if (!elementTarget.HasType())
            return false;

        if (elementSource.type.isReferenceType)
            LowerBoundInference(elementSource, elementTarget);
        else
            ExactInference(elementSource, elementTarget);

        return true;
    }

    private static TypeWithAnnotations GetMatchingElementType(ArrayTypeSymbol source, TypeSymbol target) {
        Debug.Assert(source is not null);
        Debug.Assert(target is not null);

        if (target.IsArray()) {
            var arrayTarget = (ArrayTypeSymbol)target;

            if (!arrayTarget.HasSameShapeAs(source))
                return default;

            return arrayTarget.elementTypeWithAnnotations;
        }

        if (!source.isSZArray)
            return default;

        // TODO Interfaces
        // if (!target.IsPossibleArrayGenericInterface()) {
        //     return default;
        // }

        Debug.Assert(((NamedTypeSymbol)target).templateArguments[0].isType);
        return ((NamedTypeSymbol)target).templateArguments[0].type;
    }

    private bool LowerBoundTupleInference(TypeWithAnnotations source, TypeWithAnnotations target) {
        Debug.Assert(source.HasType());
        Debug.Assert(target.HasType());

        if (!source.type.TryGetElementTypesWithAnnotationsIfTupleType(out var sourceTypes) ||
            !target.type.TryGetElementTypesWithAnnotationsIfTupleType(out var targetTypes) ||
            sourceTypes.Length != targetTypes.Length) {
            return false;
        }

        for (var i = 0; i < sourceTypes.Length; i++)
            LowerBoundInference(sourceTypes[i], targetTypes[i]);

        return true;
    }

    private bool LowerBoundConstructedInference(TypeSymbol source, TypeSymbol target) {
        Debug.Assert(source is not null);
        Debug.Assert(target is not null);

        if (target is not NamedTypeSymbol constructedTarget)
            return false;

        if (constructedTarget.AllTemplateArgumentsCount() == 0)
            return false;

        if (source is NamedTypeSymbol constructedSource &&
            TypeSymbol.Equals(
                constructedSource.originalDefinition,
                constructedTarget.originalDefinition,
                TypeCompareKind.ConsiderEverything)) {
            if (constructedSource.isInterface/* || constructedSource.IsDelegateType()*/)
                LowerBoundTemplateArgumentInference(constructedSource, constructedTarget);
            else
                ExactTemplateArgumentInference(constructedSource, constructedTarget);

            return true;
        }

        if (LowerBoundClassInference(source, constructedTarget))
            return true;

        if (LowerBoundInterfaceInference(source, constructedTarget))
            return true;

        return false;
    }

    private void LowerBoundTemplateArgumentInference(NamedTypeSymbol source, NamedTypeSymbol target) {
        Debug.Assert(source is not null);
        Debug.Assert(target is not null);
        Debug.Assert(TypeSymbol.Equals(source.originalDefinition, target.originalDefinition, TypeCompareKind.ConsiderEverything));

        var typeParameters = ArrayBuilder<TemplateParameterSymbol>.GetInstance();
        var sourceTemplateArguments = ArrayBuilder<TypeOrConstant>.GetInstance();
        var targetTemplateArguments = ArrayBuilder<TypeOrConstant>.GetInstance();

        source.originalDefinition.GetAllTemplateParameters(typeParameters);
        source.GetAllTemplateArguments(sourceTemplateArguments);
        target.GetAllTemplateArguments(targetTemplateArguments);

        Debug.Assert(typeParameters.Count == sourceTemplateArguments.Count);
        Debug.Assert(typeParameters.Count == targetTemplateArguments.Count);

        for (var arg = 0; arg < sourceTemplateArguments.Count; ++arg) {
            var typeParameter = typeParameters[arg];
            var sourceTypeArgument = sourceTemplateArguments[arg];
            var targetTypeArgument = targetTemplateArguments[arg];

            // if (sourceTypeArgument.type.isReferenceType && typeParameter.variance == VarianceKind.Out) {
            //     LowerBoundInference(sourceTypeArgument, targetTypeArgument, ref useSiteInfo);
            // } else if (sourceTypeArgument.Type.IsReferenceType && typeParameter.Variance == VarianceKind.In) {
            //     UpperBoundInference(sourceTypeArgument, targetTypeArgument, ref useSiteInfo);
            // } else {
            ExactInference(sourceTypeArgument, targetTypeArgument);
            // }
        }

        typeParameters.Free();
        sourceTemplateArguments.Free();
        targetTemplateArguments.Free();
    }

    private bool LowerBoundClassInference(TypeSymbol source, NamedTypeSymbol target) {
        Debug.Assert(source is not null);
        Debug.Assert(target is not null);

        if (target.typeKind != TypeKind.Class)
            return false;

        NamedTypeSymbol sourceBase = null;

        if (source.typeKind == TypeKind.Class)
            sourceBase = source.baseType;
        else if (source.typeKind == TypeKind.TemplateParameter)
            sourceBase = ((TemplateParameterSymbol)source).effectiveBaseClass;

        while (sourceBase is not null) {
            if (TypeSymbol.Equals(
                sourceBase.originalDefinition,
                target.originalDefinition,
                TypeCompareKind.ConsiderEverything)) {
                ExactTemplateArgumentInference(sourceBase, target);
                return true;
            }

            sourceBase = sourceBase.baseType;
        }

        return false;
    }

    private bool LowerBoundInterfaceInference(TypeSymbol source, NamedTypeSymbol target) {
        Debug.Assert(source is not null);
        Debug.Assert(target is not null);

        if (!target.isInterface)
            return false;

        ImmutableArray<NamedTypeSymbol> allInterfaces;

        switch (source.typeKind) {
            case TypeKind.Struct:
            case TypeKind.Class:
            case TypeKind.Interface:
                allInterfaces = source.allInterfaces;
                break;
            case TypeKind.TemplateParameter:
                var typeParameter = (TemplateParameterSymbol)source;

                allInterfaces = typeParameter.effectiveBaseClass.allInterfaces
                    .Concat(typeParameter.allEffectiveInterfaces);

                break;
            default:
                return false;
        }

        // allInterfaces = ModuloReferenceTypeNullabilityDifferences(allInterfaces, VarianceKind.In);

        var matchingInterface = GetInterfaceInferenceBound(allInterfaces, target);

        if (matchingInterface is null)
            return false;

        LowerBoundTemplateArgumentInference(matchingInterface, target);
        return true;
    }

    private static NamedTypeSymbol GetInterfaceInferenceBound(
        ImmutableArray<NamedTypeSymbol> interfaces,
        NamedTypeSymbol target) {
        Debug.Assert(target.isInterface);
        NamedTypeSymbol matchingInterface = null;

        foreach (var currentInterface in interfaces) {
            if (TypeSymbol.Equals(
                    currentInterface.originalDefinition,
                    target.originalDefinition,
                    TypeCompareKind.ConsiderEverything)) {
                if (matchingInterface is null) {
                    matchingInterface = currentInterface;
                } else if (!TypeSymbol.Equals(
                        matchingInterface,
                        currentInterface,
                        TypeCompareKind.ConsiderEverything)) {
                    return null;
                }
            }
        }

        return matchingInterface;
    }

    private bool LowerBoundFunctionPointerTypeInference(TypeSymbol source, TypeSymbol target) {
        if (source is not FunctionPointerTypeSymbol { signature: { } sourceSignature } ||
            target is not FunctionPointerTypeSymbol { signature: { } targetSignature }) {
            return false;
        }

        throw ExceptionUtilities.Unreachable();

        // if (sourceSignature.parameterCount != targetSignature.parameterCount)
        //     return false;

        // if (!FunctionPointerRefKindsEqual(sourceSignature, targetSignature) || !FunctionPointerCallingConventionsEqual(sourceSignature, targetSignature)) {
        //     return false;
        // }

        // // Reference parameters are treated as "input" variance by default, and reference return types are treated as out variance by default.
        // // If they have a ref kind or are not reference types, then they are treated as invariant.
        // for (int i = 0; i < sourceSignature.ParameterCount; i++) {
        //     var sourceParam = sourceSignature.Parameters[i];
        //     var targetParam = targetSignature.Parameters[i];

        //     if ((sourceParam.Type.IsReferenceType || sourceParam.Type.IsFunctionPointer()) && sourceParam.RefKind == RefKind.None) {
        //         UpperBoundInference(sourceParam.TypeWithAnnotations, targetParam.TypeWithAnnotations, ref useSiteInfo);
        //     } else {
        //         ExactInference(sourceParam.TypeWithAnnotations, targetParam.TypeWithAnnotations, ref useSiteInfo);
        //     }
        // }

        // if ((sourceSignature.ReturnType.IsReferenceType || sourceSignature.ReturnType.IsFunctionPointer()) && sourceSignature.RefKind == RefKind.None) {
        //     LowerBoundInference(sourceSignature.ReturnTypeWithAnnotations, targetSignature.ReturnTypeWithAnnotations, ref useSiteInfo);
        // } else {
        //     ExactInference(sourceSignature.ReturnTypeWithAnnotations, targetSignature.ReturnTypeWithAnnotations, ref useSiteInfo);
        // }

        // return true;
    }

    private void UpperBoundInference(TypeWithAnnotations source, TypeWithAnnotations target) {
        Debug.Assert(source.HasType());
        Debug.Assert(target.HasType());

        if (UpperBoundNullableInference(source, target))
            return;

        if (UpperBoundTemplateParameterInference(source, target))
            return;

        if (UpperBoundArrayInference(source, target))
            return;

        Debug.Assert(source.type.isReferenceType || source.type is FunctionPointerTypeSymbol);

        if (UpperBoundConstructedInference(source, target))
            return;

        if (UpperBoundFunctionPointerTypeInference(source.type, target.type))
            return;
    }

    private bool UpperBoundNullableInference(TypeWithAnnotations source, TypeWithAnnotations target) {
        return ExactOrBoundsNullableInference(ExactOrBoundsKind.UpperBound, source, target);
    }

    private bool UpperBoundTemplateParameterInference(TypeWithAnnotations source, TypeWithAnnotations target) {
        Debug.Assert(source.HasType());
        Debug.Assert(target.HasType());

        if (IsUnfixedTemplateParameter(target)) {
            AddBound(source, _upperBounds, target);
            return true;
        }

        return false;
    }

    private bool UpperBoundArrayInference(TypeWithAnnotations source, TypeWithAnnotations target) {
        Debug.Assert(source.HasType());
        Debug.Assert(target.HasType());

        if (!target.type.IsArray())
            return false;

        var arrayTarget = (ArrayTypeSymbol)target.type;
        var elementTarget = arrayTarget.elementTypeWithAnnotations;
        var elementSource = GetMatchingElementType(arrayTarget, source.type);

        if (!elementSource.HasType())
            return false;

        if (elementSource.type.isReferenceType)
            UpperBoundInference(elementSource, elementTarget);
        else
            ExactInference(elementSource, elementTarget);

        return true;
    }

    private bool UpperBoundConstructedInference(
        TypeWithAnnotations sourceWithAnnotations,
        TypeWithAnnotations targetWithAnnotations) {
        Debug.Assert(sourceWithAnnotations.HasType());
        Debug.Assert(targetWithAnnotations.HasType());
        var source = sourceWithAnnotations.type;
        var target = targetWithAnnotations.type;

        if (source is not NamedTypeSymbol constructedSource)
            return false;

        if (constructedSource.AllTemplateArgumentsCount() == 0)
            return false;

        if (target is NamedTypeSymbol constructedTarget &&
            TypeSymbol.Equals(
                constructedSource.originalDefinition,
                target.originalDefinition,
                TypeCompareKind.ConsiderEverything)) {
            if (constructedTarget.isInterface/* || constructedTarget.IsDelegateType()*/)
                UpperBoundTemplateArgumentInference(constructedSource, constructedTarget);
            else
                ExactTemplateArgumentInference(constructedSource, constructedTarget);

            return true;
        }

        if (UpperBoundClassInference(constructedSource, target))
            return true;

        if (UpperBoundInterfaceInference(constructedSource, target))
            return true;

        return false;
    }

    private void UpperBoundTemplateArgumentInference(NamedTypeSymbol source, NamedTypeSymbol target) {
        Debug.Assert(source is not null);
        Debug.Assert(target is not null);
        Debug.Assert(TypeSymbol.Equals(source.originalDefinition, target.originalDefinition, TypeCompareKind.ConsiderEverything));

        var typeParameters = ArrayBuilder<TemplateParameterSymbol>.GetInstance();
        var sourceTemplateArguments = ArrayBuilder<TypeOrConstant>.GetInstance();
        var targetTemplateArguments = ArrayBuilder<TypeOrConstant>.GetInstance();

        source.originalDefinition.GetAllTemplateParameters(typeParameters);
        source.GetAllTemplateArguments(sourceTemplateArguments);
        target.GetAllTemplateArguments(targetTemplateArguments);

        Debug.Assert(typeParameters.Count == sourceTemplateArguments.Count);
        Debug.Assert(typeParameters.Count == targetTemplateArguments.Count);

        for (var arg = 0; arg < sourceTemplateArguments.Count; ++arg) {
            var typeParameter = typeParameters[arg];
            var sourceTypeArgument = sourceTemplateArguments[arg];
            var targetTypeArgument = targetTemplateArguments[arg];

            // if (sourceTypeArgument.type.isReferenceType && typeParameter.Variance == VarianceKind.Out) {
            //     UpperBoundInference(sourceTypeArgument, targetTypeArgument, ref useSiteInfo);
            // } else if (sourceTypeArgument.Type.IsReferenceType && typeParameter.Variance == VarianceKind.In) {
            //     LowerBoundInference(sourceTypeArgument, targetTypeArgument, ref useSiteInfo);
            // } else {
            ExactInference(sourceTypeArgument, targetTypeArgument);
            // }
        }

        typeParameters.Free();
        sourceTemplateArguments.Free();
        targetTemplateArguments.Free();
    }

    private bool UpperBoundClassInference(NamedTypeSymbol source, TypeSymbol target) {
        Debug.Assert(source is not null);
        Debug.Assert(target is not null);

        if (source.typeKind != TypeKind.Class || target.typeKind != TypeKind.Class)
            return false;

        var targetBase = target.baseType;

        while (targetBase is not null) {
            if (TypeSymbol.Equals(
                    targetBase.originalDefinition,
                    source.originalDefinition,
                    TypeCompareKind.ConsiderEverything)) {
                ExactTemplateArgumentInference(source, targetBase);
                return true;
            }

            targetBase = targetBase.baseType;
        }

        return false;
    }

    private bool UpperBoundInterfaceInference(NamedTypeSymbol source, TypeSymbol target) {
        Debug.Assert(source is not null);
        Debug.Assert(target is not null);

        if (!source.isInterface)
            return false;

        switch (target.typeKind) {
            case TypeKind.Struct:
            case TypeKind.Class:
            case TypeKind.Interface:
                break;
            default:
                return false;
        }

        var allInterfaces = target.allInterfaces;

        // allInterfaces = ModuloReferenceTypeNullabilityDifferences(allInterfaces, VarianceKind.Out);

        var bestInterface = GetInterfaceInferenceBound(allInterfaces, source);

        if (bestInterface is null)
            return false;

        UpperBoundTemplateArgumentInference(source, bestInterface);
        return true;
    }

    private bool UpperBoundFunctionPointerTypeInference(TypeSymbol source, TypeSymbol target) {
        if (source is not FunctionPointerTypeSymbol { signature: { } sourceSignature } ||
            target is not FunctionPointerTypeSymbol { signature: { } targetSignature }) {
            return false;
        }

        throw ExceptionUtilities.Unreachable();

        // if (sourceSignature.ParameterCount != targetSignature.ParameterCount) {
        //     return false;
        // }

        // if (!FunctionPointerRefKindsEqual(sourceSignature, targetSignature) || !FunctionPointerCallingConventionsEqual(sourceSignature, targetSignature)) {
        //     return false;
        // }

        // // Reference parameters are treated as "input" variance by default, and reference return types are treated as out variance by default.
        // // If they have a ref kind or are not reference types, then they are treated as invariant.
        // for (int i = 0; i < sourceSignature.ParameterCount; i++) {
        //     var sourceParam = sourceSignature.Parameters[i];
        //     var targetParam = targetSignature.Parameters[i];

        //     if ((sourceParam.Type.IsReferenceType || sourceParam.Type.IsFunctionPointer()) && sourceParam.RefKind == RefKind.None) {
        //         LowerBoundInference(sourceParam.TypeWithAnnotations, targetParam.TypeWithAnnotations, ref useSiteInfo);
        //     } else {
        //         ExactInference(sourceParam.TypeWithAnnotations, targetParam.TypeWithAnnotations, ref useSiteInfo);
        //     }
        // }

        // if ((sourceSignature.ReturnType.IsReferenceType || sourceSignature.ReturnType.IsFunctionPointer()) && sourceSignature.RefKind == RefKind.None) {
        //     UpperBoundInference(sourceSignature.ReturnTypeWithAnnotations, targetSignature.ReturnTypeWithAnnotations, ref useSiteInfo);
        // } else {
        //     ExactInference(sourceSignature.ReturnTypeWithAnnotations, targetSignature.ReturnTypeWithAnnotations, ref useSiteInfo);
        // }

        // return true;
    }

    private ImmutableArray<TypeOrConstant> GetResults(out bool inferredFromFunctionType) {
        for (var i = 0; i < _methodTypeParameters.Length; i++) {
            var fixedResultTypeOrConstant = _fixedResults[i].Type;

            if (fixedResultTypeOrConstant.HasTypeOrConstant()) {
                if (fixedResultTypeOrConstant.isType) {
                    if (!fixedResultTypeOrConstant.type.type.IsErrorType()) {
                        // if (_conversions.includeNullability && _nullableAnnotationLowerBounds[i].IsAnnotated()) {
                        //     _fixedResults[i] = _fixedResults[i] with { Type = fixedResultType.AsAnnotated() };
                        // }
                        continue;
                    }

                    var errorTypeName = fixedResultTypeOrConstant.type.type.name;

                    if (errorTypeName is not null)
                        continue;
                } else {
                    continue;
                }
            }

            if (_methodTypeParameters[i].underlyingType.specialType == SpecialType.Type) {
                _fixedResults[i] = (
                    new TypeOrConstant(
                        new ExtendedErrorTypeSymbol(
                            _constructedContainingTypeOfMethod,
                            _methodTypeParameters[i].name,
                            0,
                            null,
                            false
                        )
                    ),
                    false
                );
            } else {
                _fixedResults[i] = (
                    new TypeOrConstant(constant: null),
                    false
                );
            }
        }

        return GetInferredTemplateArguments(out inferredFromFunctionType);
    }

    private ImmutableArray<TypeOrConstant> GetInferredTemplateArguments(out bool inferredFromFunctionType) {
        var builder = ArrayBuilder<TypeOrConstant>.GetInstance(_fixedResults.Length);
        inferredFromFunctionType = false;

        foreach (var fixedResult in _fixedResults) {
            builder.Add(fixedResult.Type);

            if (fixedResult.FromFunctionType)
                inferredFromFunctionType = true;
        }

        return builder.ToImmutableAndFree();
    }

    private bool InferTemplateArgsSecondPhase(Binder binder) {
        InitializeDependencies();

        while (true) {
            var res = DoSecondPhase(binder);
            Debug.Assert(res != InferenceResult.NoProgress);

            if (res == InferenceResult.InferenceFailed)
                return false;

            if (res == InferenceResult.Success)
                return true;
        }
    }

    private void InitializeDependencies() {
        Debug.Assert(_dependencies == null);
        _dependencies = new Dependency[_methodTypeParameters.Length, _methodTypeParameters.Length];
        int iParam;
        int jParam;
        Debug.Assert(0 == (int)Dependency.Unknown);

        for (iParam = 0; iParam < _methodTypeParameters.Length; iParam++) {
            for (jParam = 0; jParam < _methodTypeParameters.Length; jParam++) {
                if (DependsDirectlyOn(iParam, jParam))
                    _dependencies[iParam, jParam] = Dependency.Direct;
            }
        }

        DeduceAllDependencies();
    }

    private bool DependsDirectlyOn(int iParam, int jParam) {
        Debug.Assert(ValidIndex(iParam));
        Debug.Assert(ValidIndex(jParam));
        Debug.Assert(IsUnfixed(iParam));
        Debug.Assert(IsUnfixed(jParam));

        for (int iArg = 0, length = NumberArgumentsToProcess; iArg < length; iArg++) {
            var formalParameterType = _formalParameterTypes[iArg].type;
            var argument = _arguments[iArg];

            if (argument.isExpression) {
                if (DoesInputTypeContain(argument.expression, formalParameterType, _methodTypeParameters[jParam]) &&
                    DoesOutputTypeContain(argument.expression, formalParameterType, _methodTypeParameters[iParam])) {
                    return true;
                }
            } else {
                // TODO isConstant
            }
        }

        return false;
    }

    private static bool DoesInputTypeContain(
        BoundExpression argument,
        TypeSymbol formalParameterType,
        TemplateParameterSymbol templateParameter) {
        var functionOrFunctionPointerType = formalParameterType.GetFunctionOrFunctionPointerType();

        if (functionOrFunctionPointerType is null)
            return false;

        var isFunctionPointer = functionOrFunctionPointerType is FunctionPointerTypeSymbol;

        if ((isFunctionPointer && argument.kind != BoundKind.UnconvertedAddressOfOperator) ||
            (!isFunctionPointer && argument.kind is not (BoundKind.UnboundLambda or BoundKind.MethodGroup))) {
            return false;
        }

        var parameters = functionOrFunctionPointerType.FunctionOrFunctionPointerParameters();

        if (parameters.IsDefaultOrEmpty)
            return false;

        foreach (var parameter in parameters) {
            if (parameter.type.ContainsTemplateParameter(templateParameter))
                return true;
        }

        return false;
    }

    private static bool DoesOutputTypeContain(
        BoundExpression argument,
        TypeSymbol formalParameterType,
        TemplateParameterSymbol templateParameter) {
        var functionOrFunctionPointerType = formalParameterType.GetFunctionOrFunctionPointerType();

        if (functionOrFunctionPointerType is null)
            return false;

        var isFunctionPointer = functionOrFunctionPointerType is FunctionPointerTypeSymbol;

        if ((isFunctionPointer && argument.kind != BoundKind.UnconvertedAddressOfOperator) ||
            (!isFunctionPointer && argument.kind is not (BoundKind.UnboundLambda or BoundKind.MethodGroup))) {
            return false;
        }

        MethodSymbol method = functionOrFunctionPointerType switch {
            FunctionTypeSymbol n => n.signature,
            FunctionPointerTypeSymbol f => f.signature,
            _ => throw ExceptionUtilities.UnexpectedValue(functionOrFunctionPointerType)
        };

        if (method is null)
            return false;

        var returnType = method.returnType;

        if (returnType is null)
            return false;

        return returnType.ContainsTemplateParameter(templateParameter);
    }

    private void DeduceAllDependencies() {
        bool madeProgress;

        do {
            madeProgress = DeduceDependencies();
        } while (madeProgress);

        SetUnknownsToNotDependent();
        _dependenciesDirty = false;
    }

    private bool DeduceDependencies() {
        Debug.Assert(_dependencies != null);
        var madeProgress = false;

        for (var iParam = 0; iParam < _methodTypeParameters.Length; iParam++) {
            for (var jParam = 0; jParam < _methodTypeParameters.Length; jParam++) {
                if (_dependencies[iParam, jParam] == Dependency.Unknown) {
                    if (DependsTransitivelyOn(iParam, jParam)) {
                        _dependencies[iParam, jParam] = Dependency.Indirect;
                        madeProgress = true;
                    }
                }
            }
        }
        return madeProgress;
    }

    private void SetUnknownsToNotDependent() {
        Debug.Assert(_dependencies != null);

        for (var iParam = 0; iParam < _methodTypeParameters.Length; iParam++) {
            for (var jParam = 0; jParam < _methodTypeParameters.Length; jParam++) {
                if (_dependencies[iParam, jParam] == Dependency.Unknown)
                    _dependencies[iParam, jParam] = Dependency.NotDependent;
            }
        }
    }

    private bool DependsTransitivelyOn(int iParam, int jParam) {
        Debug.Assert(_dependencies is not null);
        Debug.Assert(ValidIndex(iParam));
        Debug.Assert(ValidIndex(jParam));

        for (var kParam = 0; kParam < _methodTypeParameters.Length; ++kParam) {
            if ((_dependencies[iParam, kParam] & Dependency.DependsMask) != 0 &&
                (_dependencies[kParam, jParam] & Dependency.DependsMask) != 0) {
                return true;
            }
        }

        return false;
    }

    private InferenceResult DoSecondPhase(Binder binder) {
        if (AllFixed())
            return InferenceResult.Success;

        MakeOutputTypeInferences(binder);

        InferenceResult res;
        res = FixNondependentParameters();

        if (res != InferenceResult.NoProgress)
            return res;

        res = FixDependentParameters();

        if (res != InferenceResult.NoProgress)
            return res;

        res = ApplyTemplateDefaults();

        if (res != InferenceResult.NoProgress)
            return res;

        return InferenceResult.InferenceFailed;
    }

    private InferenceResult ApplyTemplateDefaults() {
        // This is only reachable when type interference would have otherwise failed
        // so we can fill in all template parameter default values at once

        var madeProgress = 0;

        for (var i = 0; i < _fixedResults.Length; i++) {
            if (_fixedResults[i].Type.HasTypeOrConstant())
                continue;

            if (_methodTypeParameters[i].defaultValue is null)
                continue;

            var defaultValue = _methodTypeParameters[i].defaultValue;

            if (_constructedContainingTypeOfMethod.templateSubstitution is not null)
                defaultValue = defaultValue.Substitute(_constructedContainingTypeOfMethod.templateSubstitution);

            // TODO These asserts might actually be reachable and instead we just want to fail inference
            Debug.Assert(!(defaultValue.isConstant && defaultValue.constant is null));
            Debug.Assert(!(defaultValue.isType && !defaultValue.type.HasType()));

            _fixedResults[i] = (defaultValue, true);
            madeProgress++;
        }

        if (madeProgress == 0)
            return InferenceResult.NoProgress;
        else if (madeProgress == _fixedResults.Length)
            return InferenceResult.Success;
        else
            return InferenceResult.MadeProgress;
    }

    private void MakeOutputTypeInferences(Binder binder) {
        for (int arg = 0, length = NumberArgumentsToProcess; arg < length; arg++) {
            var formalType = _formalParameterTypes[arg];
            var argument = _arguments[arg];
            MakeOutputTypeInferences(binder, argument, formalType);
        }
    }

    private void MakeOutputTypeInferences(
        Binder binder,
        BoundExpressionOrTypeOrConstant argument,
        TypeWithAnnotations formalType) {
        if (argument.isExpression)
            MakeOutputTypeInferences(binder, argument.expression, formalType);

        // TODO
    }

    private void MakeOutputTypeInferences(Binder binder, BoundExpression argument, TypeWithAnnotations formalType) {
        if (argument.kind == BoundKind.TupleLiteral && (object)argument.Type is null) {
            MakeOutputTypeInferences(binder, (BoundTupleLiteral)argument, formalType);
        } else if (argument.kind == BoundKind.UnconvertedInitializerList) {
            MakeOutputTypeInferences(binder, (BoundUnconvertedInitializerList)argument, formalType);
        } else {
            if (HasUnfixedParamInOutputType(argument, formalType.type) &&
                !HasUnfixedParamInInputType(argument, formalType.type)) {
                OutputTypeInference(binder, argument, formalType);
            }
        }
    }

    private bool HasUnfixedParamInOutputType(BoundExpression argument, TypeSymbol formalParameterType) {
        for (var iParam = 0; iParam < _methodTypeParameters.Length; iParam++) {
            if (IsUnfixed(iParam)) {
                if (DoesOutputTypeContain(argument, formalParameterType, _methodTypeParameters[iParam]))
                    return true;
            }
        }

        return false;
    }

    private bool HasUnfixedParamInInputType(BoundExpression pSource, TypeSymbol pDest) {
        for (var iParam = 0; iParam < _methodTypeParameters.Length; iParam++) {
            if (IsUnfixed(iParam)) {
                if (DoesInputTypeContain(pSource, pDest, _methodTypeParameters[iParam]))
                    return true;
            }
        }

        return false;
    }

    private InferenceResult FixNondependentParameters() {
        return FixParameters((ref MethodTypeInferrer inferrer, int index) => !inferrer.DependsOnAny(index));
    }

    private InferenceResult FixDependentParameters() {
        return FixParameters((ref MethodTypeInferrer inferrer, int index) => inferrer.AnyDependsOn(index));
    }

    private bool DependsOnAny(int iParam) {
        Debug.Assert(ValidIndex(iParam));
        for (var jParam = 0; jParam < _methodTypeParameters.Length; ++jParam) {
            if (DependsOn(iParam, jParam)) {
                return true;
            }
        }

        return false;
    }

    private bool AnyDependsOn(int iParam) {
        Debug.Assert(ValidIndex(iParam));
        for (var jParam = 0; jParam < _methodTypeParameters.Length; ++jParam) {
            if (DependsOn(jParam, iParam)) {
                return true;
            }
        }

        return false;
    }

    private bool DependsOn(int iParam, int jParam) {
        Debug.Assert(_dependencies != null);
        Debug.Assert(0 <= iParam && iParam < _methodTypeParameters.Length);
        Debug.Assert(0 <= jParam && jParam < _methodTypeParameters.Length);

        if (_dependenciesDirty) {
            SetIndirectsToUnknown();
            DeduceAllDependencies();
        }

        return 0 != (_dependencies[iParam, jParam] & Dependency.DependsMask);
    }

    private void SetIndirectsToUnknown() {
        Debug.Assert(_dependencies is not null);

        for (var iParam = 0; iParam < _methodTypeParameters.Length; iParam++) {
            for (var jParam = 0; jParam < _methodTypeParameters.Length; jParam++) {
                if (_dependencies[iParam, jParam] == Dependency.Indirect)
                    _dependencies[iParam, jParam] = Dependency.Unknown;
            }
        }
    }

    private delegate bool FixParametersPredicate(ref MethodTypeInferrer inferrer, int index);

    private InferenceResult FixParameters(FixParametersPredicate predicate) {
        var needsFixing = BitVector.Create(_methodTypeParameters.Length);
        var result = InferenceResult.NoProgress;

        for (var param = 0; param < _methodTypeParameters.Length; param++) {
            if (IsUnfixed(param) && HasBound(param) && predicate(ref this, param)) {
                needsFixing[param] = true;
                result = InferenceResult.MadeProgress;
            }
        }

        for (var param = 0; param < _methodTypeParameters.Length; param++) {
            if (needsFixing[param]) {
                if (!Fix(param))
                    result = InferenceResult.InferenceFailed;
            }
        }
        return result;
    }

    private bool Fix(int iParam) {
        Debug.Assert(IsUnfixed(iParam));

        var typeParameter = _methodTypeParameters[iParam];
        var exact = _exactBounds[iParam];
        var lower = _lowerBounds[iParam];
        var upper = _upperBounds[iParam];

        var best = Fix(_compilation, _conversions, typeParameter, exact, lower, upper);

        if (!best.Type.HasTypeOrConstant())
            return false;

        _fixedResults[iParam] = best;
        UpdateDependenciesAfterFix(iParam);
        return true;
    }

    private void UpdateDependenciesAfterFix(int iParam) {
        Debug.Assert(ValidIndex(iParam));

        if (_dependencies is null)
            return;

        for (var jParam = 0; jParam < _methodTypeParameters.Length; ++jParam) {
            _dependencies[iParam, jParam] = Dependency.NotDependent;
            _dependencies[jParam, iParam] = Dependency.NotDependent;
        }

        _dependenciesDirty = true;
    }

    private static (TypeOrConstant Type, bool FromFunctionType) Fix(
        Compilation compilation,
        ConversionsBase conversions,
        TemplateParameterSymbol typeParameter,
        HashSet<TypeOrConstant> exact,
        HashSet<TypeOrConstant> lower,
        HashSet<TypeOrConstant> upper) {
        var candidates = new Dictionary<TypeOrConstant, TypeOrConstant>(
            EqualsIgnoringTupleNamesComparer.Instance
        );

        Debug.Assert(!ContainsFunctionTypes(exact));
        Debug.Assert(!ContainsFunctionTypes(upper));

        Predicate<TypeOrConstant> lowerPredicate;

        if (ContainsFunctionTypes(lower) &&
            (ContainsNonFunctionTypes(lower) || ContainsNonFunctionTypes(exact) || ContainsNonFunctionTypes(upper))) {
            lowerPredicate = static type => !IsFunctionType(type, out _);
        } else {
            lowerPredicate = static type => !IsFunctionType(type, out var functionType) ||
                functionType is not null;
        }

        if (exact is null) {
            if (lower is not null)
                AddAllCandidates(candidates, lower, lowerPredicate/*, VarianceKind.Out*/, conversions);

            if (upper is not null)
                AddAllCandidates(candidates, upper, predicate: null/*, VarianceKind.In*/, conversions);

        } else {
            AddAllCandidates(candidates, exact, predicate: null/*, VarianceKind.None*/, conversions);

            if (candidates.Count >= 2)
                return default;
        }

        if (candidates.Count == 0)
            return default;

        var initialCandidates = ArrayBuilder<TypeOrConstant>.GetInstance();
        GetAllCandidates(candidates, initialCandidates);

        if (lower is not null)
            MergeOrRemoveCandidates(candidates, lower, lowerPredicate, initialCandidates, conversions/*, VarianceKind.Out*/);

        if (upper is not null)
            MergeOrRemoveCandidates(candidates, upper, predicate: null, initialCandidates, conversions/*, VarianceKind.In*/);

        initialCandidates.Clear();
        GetAllCandidates(candidates, initialCandidates);

        TypeOrConstant best = default;

        foreach (var candidate in initialCandidates) {
            foreach (var candidate2 in initialCandidates) {
                if (!candidate.Equals(candidate2, TypeCompareKind.ConsiderEverything) &&
                    !ImplicitConversionExists(candidate2, candidate, conversions)) {
                    goto OuterBreak;
                }
            }

            if (!best.HasTypeOrConstant()) {
                best = candidate;
            } else {
                Debug.Assert(!best.Equals(candidate, TypeCompareKind.IgnoreTupleNames));
                best = default;
                break;
            }

OuterBreak:
            ;
        }

        initialCandidates.Free();

        var fromFunctionType = false;

        if (IsFunctionType(best, out var functionType)) {
            var resultType = functionType;

            if (HasExpressionTypeConstraint(typeParameter)) {
                Debug.Assert(compilation is not null);
                throw ExceptionUtilities.Unreachable();
                // var expressionOfTType = compilation.GetWellKnownType(WellKnownType.System_Linq_Expressions_Expression_T);
                // resultType = expressionOfTType.Construct(resultType);
            }

            best = new TypeOrConstant(new TypeWithAnnotations(resultType, best.type.isNullable));
            fromFunctionType = true;
        }

        return (best, fromFunctionType);

        static bool ContainsFunctionTypes(HashSet<TypeOrConstant> types) {
            return types?.Any(t => IsFunctionType(t, out _)) == true;
        }

        static bool ContainsNonFunctionTypes(HashSet<TypeOrConstant> types) {
            return types?.Any(t => !IsFunctionType(t, out _)) == true;
        }

        static bool IsFunctionType(TypeOrConstant type, out FunctionTypeSymbol functionType) {
            functionType = type?.type?.type as FunctionTypeSymbol;
            return functionType is not null;
        }

        static bool HasExpressionTypeConstraint(TemplateParameterSymbol typeParameter) {
            var constraintTypes = typeParameter.constraintTypes;
            return constraintTypes.Any(static t => IsExpressionType(t.type));
        }

        static bool IsExpressionType(TypeSymbol type) {
            while (type is { }) {
                // if (type.IsGenericOrNonGenericExpressionType(out _)) {
                //     return true;
                // }

                type = type.baseType;
            }

            return false;
        }
    }

    private static bool ImplicitConversionExists(
        TypeOrConstant sourceTypeOrConstant,
        TypeOrConstant destinationTypeOrConstant,
        ConversionsBase conversions) {
        if (sourceTypeOrConstant.isType && destinationTypeOrConstant.isType) {
            return ImplicitConversionExistsCore(sourceTypeOrConstant.type, destinationTypeOrConstant.type, conversions);
        } else {
            Debug.Assert(sourceTypeOrConstant.isConstant && destinationTypeOrConstant.isConstant);

            var sourceType = new TypeWithAnnotations(
                CorLibrary.GetSpecialType(sourceTypeOrConstant.constant.specialType)
            );

            var destinationType = new TypeWithAnnotations(
                CorLibrary.GetSpecialType(destinationTypeOrConstant.constant.specialType)
            );

            return ImplicitConversionExistsCore(sourceType, destinationType, conversions);
        }

        static bool ImplicitConversionExistsCore(
            TypeWithAnnotations sourceWithAnnotations,
            TypeWithAnnotations destinationWithAnnotations,
            ConversionsBase conversions) {
            var source = sourceWithAnnotations.type;
            var destination = destinationWithAnnotations.type;

            var conversion = conversions.ClassifyImplicitConversionFromTypeWhenNeitherOrBothFunctionTypes(
                source,
                destination
            );

            return conversion.exists &&
                (conversion.isUserDefined ||
                    conversions.HasTopLevelNullabilityImplicitConversion(
                        sourceWithAnnotations,
                        destinationWithAnnotations
                    )
                );
        }
    }

    private static void MergeOrRemoveCandidates(
        Dictionary<TypeOrConstant, TypeOrConstant> candidates,
        HashSet<TypeOrConstant> bounds,
        Predicate<TypeOrConstant>? predicate,
        ArrayBuilder<TypeOrConstant> initialCandidates,
        ConversionsBase conversions
        // VarianceKind variance,
        ) {
        // var comparison = conversions.includeNullability ? TypeCompareKind.ConsiderEverything : TypeCompareKind.IgnoreNullableModifiersForReferenceTypes;
        var comparison = TypeCompareKind.ConsiderEverything;

        foreach (var bound in bounds) {
            if (predicate is not null && !predicate(bound))
                continue;

            foreach (var candidate in initialCandidates) {
                if (bound.Equals(candidate, comparison))
                    continue;

                TypeOrConstant source;
                TypeOrConstant destination;

                // if (variance == VarianceKind.Out) {
                //     source = bound;
                //     destination = candidate;
                // } else {
                source = candidate;
                destination = bound;
                // }

                if (!ImplicitConversionExists(source, destination, conversions)) {
                    candidates.Remove(candidate);

                    if (candidates.TryGetValue(bound, out var oldBound)) {
                        // merge the nullability from candidate into bound
                        // var oldAnnotation = oldBound.isNullable;
                        // var newAnnotation = oldAnnotation.MergeNullableAnnotation(candidate.isNullable, variance);

                        // if (oldAnnotation != newAnnotation) {
                        if (oldBound.isType) {
                            var newBound = new TypeOrConstant(
                                new TypeWithAnnotations(
                                    oldBound.type.type,
                                    oldBound.type.isNullable || candidate.type.isNullable
                                )
                            );

                            candidates[bound] = newBound;
                        } else {
                            // TODO Any transformations here?
                            candidates[bound] = oldBound;
                        }
                        // }
                    }
                } else if (bound.Equals(candidate, TypeCompareKind.IgnoreTupleNames)) {
                    MergeAndReplaceIfStillCandidate(candidates, candidate, bound/*, variance*/);
                }
            }
        }
    }

    private static void GetAllCandidates(
        Dictionary<TypeOrConstant, TypeOrConstant> candidates,
        ArrayBuilder<TypeOrConstant> builder) {
        builder.EnsureCapacity(builder.Count + candidates.Count);

        foreach (var (_, value) in candidates)
            builder.Add(value);
    }

    private static void AddAllCandidates(
        Dictionary<TypeOrConstant, TypeOrConstant> candidates,
        HashSet<TypeOrConstant> bounds,
        Predicate<TypeOrConstant>? predicate,
        // VarianceKind variance,
        ConversionsBase conversions) {
        foreach (var candidate in bounds) {
            if (predicate is not null && !predicate(candidate))
                continue;

            var type = candidate;

            // if (!conversions.includeNullability) {
            //     // https://github.com/dotnet/roslyn/issues/30534: Should preserve
            //     // distinct "not computed" state from initial binding.
            //     type = type.SetUnknownNullabilityForReferenceTypes();
            // }

            AddOrMergeCandidate(candidates, type/*, variance*/);
        }
    }

    private static void AddOrMergeCandidate(
        Dictionary<TypeOrConstant, TypeOrConstant> candidates,
        TypeOrConstant newCandidate
        // VarianceKind variance
        ) {
        if (candidates.TryGetValue(newCandidate, out var oldCandidate))
            MergeAndReplaceIfStillCandidate(candidates, oldCandidate, newCandidate/*, variance*/);
        else
            candidates.Add(newCandidate, newCandidate);
    }

    private static void MergeAndReplaceIfStillCandidate(
        Dictionary<TypeOrConstant, TypeOrConstant> candidates,
        TypeOrConstant oldCandidate,
        TypeOrConstant newCandidate
        // VarianceKind variance
        ) {
        if (candidates.TryGetValue(oldCandidate, out var latest)) {
            // TypeWithAnnotations merged = latest.MergeEquivalentTypes(newCandidate, variance);
            var merged = latest;
            candidates[oldCandidate] = merged;
        }
    }

    private bool HasBound(int methodTypeParameterIndex) {
        Debug.Assert(ValidIndex(methodTypeParameterIndex));
        return _lowerBounds[methodTypeParameterIndex] is not null ||
            _upperBounds[methodTypeParameterIndex] is not null ||
            _exactBounds[methodTypeParameterIndex] is not null;
    }

    private void OutputTypeInference(
        Binder binder,
        BoundExpression expression,
        TypeWithAnnotations target) {
        Debug.Assert(expression is not null);
        Debug.Assert(target.HasType());
        if (InferredReturnTypeInference(expression, target))
            return;

        if (MethodGroupReturnTypeInference(binder, expression, target.type))
            return;

        var sourceType = _extensions.GetTypeWithAnnotations(expression);

        if (sourceType.HasType())
            LowerBoundInference(sourceType, target);
    }

    private bool InferredReturnTypeInference(BoundExpression source, TypeWithAnnotations target) {
        Debug.Assert(source is not null);
        Debug.Assert(target.HasType());

        if (target.type is not FunctionTypeSymbol functionType)
            return false;

        var returnType = functionType.signature.returnTypeWithAnnotations;

        if (!returnType.HasType() || returnType.specialType == SpecialType.Void)
            return false;

        var inferredReturnType = InferReturnType(source, functionType);

        if (!inferredReturnType.HasType())
            return false;

        Debug.Assert(inferredReturnType.type is not FunctionTypeSymbol);

        LowerBoundInference(inferredReturnType, returnType);
        return true;
    }

    private bool MethodGroupReturnTypeInference(Binder binder, BoundExpression source, TypeSymbol target) {
        Debug.Assert(source is not null);
        Debug.Assert(target is not null);

        if (source.kind is not (BoundKind.MethodGroup or BoundKind.UnconvertedAddressOfOperator))
            return false;

        var functionOrFunctionPointerType = target.GetFunctionOrFunctionPointerType();

        if (functionOrFunctionPointerType is null)
            return false;

        if (functionOrFunctionPointerType is FunctionPointerTypeSymbol !=
                (source.kind == BoundKind.UnconvertedAddressOfOperator)) {
            return false;
        }

        var (method, isFunctionPointerResolution) = functionOrFunctionPointerType switch {
            FunctionTypeSymbol n => ((MethodSymbol)n.signature, false),
            FunctionPointerTypeSymbol f => (f.signature, true),
            _ => throw ExceptionUtilities.UnexpectedValue(functionOrFunctionPointerType),
        };

        var sourceReturnType = method.returnTypeWithAnnotations;

        if (!sourceReturnType.HasType() || sourceReturnType.specialType == SpecialType.Void)
            return false;

        var fixedParameters = GetFixedFunctionOrFunctionPointer(functionOrFunctionPointerType)
            .FunctionOrFunctionPointerParameters();

        if (fixedParameters.IsDefault)
            return false;

        // var callingConventionInfo = isFunctionPointerResolution
        //     ? new CallingConventionInfo(method.CallingConvention, ((FunctionPointerMethodSymbol)method).GetCallingConventionModifiers())
        //     : default;
        var originalMethodGroup = source as BoundMethodGroup ?? ((BoundUnconvertedAddressOfOperator)source).operand;

        var returnType = MethodGroupReturnType(
            binder,
            originalMethodGroup,
            fixedParameters,
            method.refKind,
            isFunctionPointerResolution
        // in callingConventionInfo
        );

        if (returnType is null || returnType.IsVoidType())
            return false;

        LowerBoundInference(returnType, sourceReturnType);
        return true;
    }

    private TypeWithAnnotations MethodGroupReturnType(
        Binder binder, BoundMethodGroup source,
        ImmutableArray<ParameterSymbol> delegateParameters,
        RefKind delegateRefKind,
        bool isFunctionPointerResolution) {
        var analyzedArguments = AnalyzedArguments.GetInstance();
        Conversions.GetFunctionOrFunctionPointerArguments(
            source.syntax,
            analyzedArguments,
            delegateParameters,
            binder.compilation
        );

        var resolution = binder.ResolveMethodGroup(
            source,
            analyzedArguments,
            returnType: null
        );

        TypeWithAnnotations type = default;

        if (!resolution.isEmpty) {
            var result = resolution.overloadResolutionResult;

            if (result.succeeded)
                type = _extensions.GetMethodGroupResultType(source, result.bestResult.member);
        }

        analyzedArguments.Free();
        resolution.Free();
        return type;
    }

    private TypeSymbol GetFixedFunctionOrFunctionPointer(TypeSymbol functionOrFunctionPointerType) {
        Debug.Assert(functionOrFunctionPointerType is not null);
        Debug.Assert(functionOrFunctionPointerType is FunctionTypeSymbol or FunctionPointerTypeSymbol);

        var fixedArguments = _methodTypeParameters.SelectAsArray(
            static (typeParameter, i, self)
                => self.IsUnfixed(i)
                    ? new TypeOrConstant(typeParameter)
                    : self._fixedResults[i].Type,
            this
        );

        var typeMap = new TemplateMap(_constructedContainingTypeOfMethod, _methodTypeParameters, fixedArguments);
        var typeOrConstant = typeMap.SubstituteType(functionOrFunctionPointerType);

        Debug.Assert(typeOrConstant.isType);

        return typeOrConstant.type.type;
    }

    private TypeWithAnnotations InferReturnType(BoundExpression source, FunctionTypeSymbol target) {
        Debug.Assert(target is not null);
        Debug.Assert(!HasUnfixedParamInInputType(source, target));

        if (source.kind != BoundKind.UnboundLambda)
            return default;

        // TODO Lambdas
        throw ExceptionUtilities.Unreachable();
        // var anonymousFunction = (UnboundLambda)source;
        // if (anonymousFunction.hasSignature) {
        //     // Optimization:
        //     // We know that the anonymous function has a parameter list. If it does not
        //     // have the same arity as the delegate, then it cannot possibly be applicable.
        //     // Rather than have type inference fail, we will simply not make a return
        //     // type inference and have type inference continue on.  Either inference
        //     // will fail, or we will infer a nonapplicable method. Either way, there
        //     // is no change to the semantics of overload resolution.

        //     var originalDelegateParameters = target.DelegateParameters();
        //     if (originalDelegateParameters.IsDefault) {
        //         return default;
        //     }

        //     if (originalDelegateParameters.Length != anonymousFunction.ParameterCount) {
        //         return default;
        //     }
        // }

        // var fixedDelegate = (NamedTypeSymbol)GetFixedDelegateOrFunctionPointer(target);
        // var fixedDelegateParameters = fixedDelegate.DelegateParameters();
        // // Optimization:
        // // Similarly, if we have an entirely fixed delegate and an explicitly typed
        // // anonymous function, then the parameter types had better be identical.
        // // If not, applicability will eventually fail, so there is no semantic
        // // difference caused by failing to make a return type inference.
        // if (anonymousFunction.HasExplicitlyTypedParameterList) {
        //     for (int p = 0; p < anonymousFunction.ParameterCount; ++p) {
        //         if (!anonymousFunction.ParameterType(p).Equals(fixedDelegateParameters[p].Type, TypeCompareKind.IgnoreDynamicAndTupleNames | TypeCompareKind.IgnoreNullableModifiersForReferenceTypes)) {
        //             return default;
        //         }
        //     }
        // }

        // // Future optimization: We could return default if the delegate has out or ref parameters
        // // and the anonymous function is an implicitly typed lambda. It will not be applicable.

        // // We have an entirely fixed delegate parameter list, which is of the same arity as
        // // the anonymous function parameter list, and possibly exactly the same types if
        // // the anonymous function is explicitly typed.  Make an inference from the
        // // delegate parameters to the return type.

        // var returnType = anonymousFunction.InferReturnType(_conversions, fixedDelegate, ref useSiteInfo, out bool inferredFromFunctionType);
        // if (inferredFromFunctionType) {
        //     return default;
        // }
        // return returnType;
    }
}
