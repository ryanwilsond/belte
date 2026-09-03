using System.Collections.Immutable;
using System.Diagnostics;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Binding;

internal sealed class Conversions : ConversionsBase {
    private readonly Binder _binder;

    internal Conversions(Binder binder) {
        _binder = binder;
    }

    internal static void GetFunctionOrFunctionPointerArguments(
        SyntaxNode syntax,
        AnalyzedArguments analyzedArguments,
        ImmutableArray<ParameterSymbol> parameters,
        Compilation compilation) {
        foreach (var p in parameters) {
            var parameter = p;
            analyzedArguments.arguments.Add(
                new BoundExpressionOrTypeOrConstant(new BoundParameterExpression(syntax, parameter, null, parameter.type))
            );
            analyzedArguments.refKinds.Add(parameter.refKind);
        }
    }

    internal override Conversion GetListExpressionConversion(
        BoundUnconvertedInitializerList node,
        TypeSymbol targetType) {
        var listTypeKind = GetListExpressionTypeKind(
            _binder.compilation,
            targetType,
            out var elementTypeWithAnnotations
        );

        var elementType = elementTypeWithAnnotations?.type;

        switch (listTypeKind) {
            case ListExpressionTypeKind.None:
                return Conversion.None;
        }

        var items = node.items;

        var builder = ArrayBuilder<Conversion>.GetInstance(items.Length);

        foreach (var element in items) {
            var elementConversion = ClassifyImplicitConversionFromExpression(element, elementType);

            if (!elementConversion.exists) {
                builder.Free();
                return Conversion.None;
            }

            builder.Add(elementConversion);
        }

        return Conversion.CreateListExpressionConversion(listTypeKind, elementType, builder.ToImmutableAndFree());
    }

    internal override Conversion GetImplicitExtendedLiteralExpressionConversion(
        BoundUnconvertedExtendedLiteralExpression extended,
        TypeSymbol destination) {
        var extendedConversion = GetExtendedLiteralExpressionConversion(_binder, extended, destination);

        if (extendedConversion.exists)
            return extendedConversion;

        return Conversion.None;
    }

    internal override Conversion GetMethodGroupConversion(BoundMethodGroup source, TypeSymbol destination) {
        if (destination.StrippedType().typeKind != TypeKind.Function)
            return Conversion.None;

        var methodSymbol = (destination.StrippedType() as FunctionTypeSymbol).signature;
        var resolution = ResolveMethodGroup(_binder, source, methodSymbol);
        var conversion = (resolution.isEmpty || resolution.hasAnyErrors)
            ? Conversion.None
            : ToConversion(resolution.overloadResolutionResult, resolution.methodGroup, methodSymbol.parameterCount);

        resolution.Free();
        return conversion;
    }

    private static void GetFunctionArguments(
        SyntaxNode syntax,
        AnalyzedArguments analyzedArguments,
        ImmutableArray<ParameterSymbol> delegateParameters) {
        foreach (var p in delegateParameters) {
            var parameter = p;
            analyzedArguments.arguments.Add(new BoundExpressionOrTypeOrConstant(
                new BoundParameterExpression(syntax, parameter, null, parameter.type)
            ));
            analyzedArguments.refKinds.Add(parameter.refKind);
        }
    }

    private static MethodGroupResolution ResolveMethodGroup(
        Binder binder,
        BoundMethodGroup source,
        MethodSymbol functionMethod) {
        if (functionMethod is not null) {
            var analyzedArguments = AnalyzedArguments.GetInstance();
            GetFunctionArguments(source.syntax, analyzedArguments, functionMethod.parameters);
            var resolution = binder.ResolveMethodGroup(
                source,
                analyzedArguments,
                functionMethod.refKind,
                functionMethod.returnType,
                true
            );

            analyzedArguments.Free();
            return resolution;
        } else {
            return binder.ResolveMethodGroup(source, analyzedArguments: null);
        }
    }

    internal static bool ReportMethodGroupDiagnostics(
        Binder binder,
        BoundMethodGroup expr,
        TypeSymbol targetType,
        BelteDiagnosticQueue diagnostics) {
        if (targetType.StrippedType() is not FunctionTypeSymbol s)
            return false;

        var resolution = ResolveMethodGroup(binder, expr, s.signature);
        var hasErrors = resolution.hasAnyErrors;

        if (resolution.methodGroup is not null) {
            var result = resolution.overloadResolutionResult;

            if (result is not null) {
                if (result.succeeded) {
                } else if (!hasErrors && !resolution.isEmpty && resolution.resultKind == LookupResultKind.Viable) {
                    var overloadDiagnostics = BelteDiagnosticQueue.GetInstance();
                    result.ReportDiagnostics(
                        binder: binder,
                        location: expr.syntax.location,
                        node: expr.syntax,
                        diagnostics: overloadDiagnostics,
                        name: expr.name,
                        receiver: resolution.methodGroup.receiver,
                        invokedExpression: expr.syntax,
                        arguments: resolution.analyzedArguments,
                        memberGroup: resolution.methodGroup.methods.ToImmutable(),
                        typeContainingConstructor: null,
                        isMethodGroupConversion: true,
                        returnRefKind: s.signature?.refKind,
                        functionTypeSymbol: s
                    );

                    hasErrors = overloadDiagnostics.AnyErrors();
                    diagnostics.PushRangeAndFree(overloadDiagnostics);
                }
            }
        }

        resolution.Free();
        return hasErrors;
    }

    internal static bool TryToConstructUserDefinedOperator(
        Binder binder,
        Conversions conversions,
        MethodSymbol op,
        ImmutableArray<BoundExpressionOrTypeOrConstant> arguments,
        ImmutableArray<TypeWithAnnotations> parameterTypes,
        ImmutableArray<RefKind> parameterRefKinds,
        TypeSymbol returnType,
        out MethodSymbol result) {
        var originalTemplateParameters = op.templateParameters;

        var ordinals = op.MakeAdjustedTemplateParameterOrdinalsIfNeeded(originalTemplateParameters);

        var inferenceResult = MethodTypeInferrer.Infer(
            binder,
            conversions,
            originalTemplateParameters,
            op.containingType,
            parameterTypes,
            parameterRefKinds,
            arguments,
            formalReturnType: op.returnType,
            returnTargetType: returnType,
            ordinals: ordinals
        );

        if (inferenceResult.success) {
            result = op.Construct(inferenceResult.inferredTypeArguments);

            var impliedConstraints = binder.GetEnclosingTemplateConstraints();

            for (var i = 0; i < parameterTypes.Length; i++) {
                var _ = BelteDiagnosticQueue.GetInstance();
                parameterTypes[i].type.CheckAllConstraints(
                    conversions,
                    result.parameters[i].location,
                    impliedConstraints,
                    _
                );

                if (_.Any()) {
                    _.Free();
                    result = null;
                    return false;
                }

                _.Free();
            }

            var _1 = BelteDiagnosticQueue.GetInstance();

            var constraintsSatisfied = ConstraintsHelpers.CheckMethodConstraints(
                result,
                conversions,
                arguments[0].syntax?.location,
                impliedConstraints,
                _1
            );

            Debug.Assert(constraintsSatisfied != _1.Any());

            if (_1.Any()) {
                _1.Free();
                result = null;
                return false;
            }

            _1.Free();
            return true;
        }

        result = null;
        return false;
    }

    private protected override bool TryToConstructUserDefinedOperator(
        MethodSymbol op,
        BoundExpression argument,
        TypeSymbol source,
        TypeSymbol target,
        out MethodSymbol result) {
        // If the argument is null, we are just checking if something exists but don't actually care about diagnostics
        // So the location doesn't matter
        var arg = argument is not null
            ? new BoundExpressionOrTypeOrConstant(argument)
            : new BoundExpressionOrTypeOrConstant(new BoundValuePlaceholder(null, source));

        return TryToConstructUserDefinedOperator(
            _binder,
            this,
            op,
            [arg],
            [new TypeWithAnnotations(source)],
            [RefKind.None],
            target,
            out result
        );
    }

    private protected override Conversion GetImplicitArrayLengthConversion(
        BoundUnconvertedArrayLength length,
        TypeSymbol destination) {
        if (destination.specialType is SpecialType.Int32 or SpecialType.Int64 or SpecialType.Int)
            return Conversion.Identity;

        var conversion = ClassifyConversionFromType(_binder.compilation.GetSpecialType(SpecialType.Int), destination);

        if (conversion.isImplicit)
            return conversion;

        return Conversion.None;
    }
}
