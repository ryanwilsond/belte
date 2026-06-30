using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Binding;

internal sealed class Conversions : ConversionsBase {
    private readonly Binder _binder;

    internal Conversions(Binder binder) {
        _binder = binder;
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
}
