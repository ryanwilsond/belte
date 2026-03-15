using System;
using System.Collections.Immutable;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Symbols;

internal static class ParameterHelpers {

    internal static ImmutableArray<SourceParameterSymbol> MakeParameters(
        Binder withTemplateParametersBinder,
        Symbol owner,
        SeparatedSyntaxList<ParameterSyntax> parameterList,
        BelteDiagnosticQueue diagnostics,
        bool allowRef,
        bool addRefConstModifier) {
        return MakeParameters<SourceParameterSymbol, Symbol>(
            withTemplateParametersBinder,
            owner,
            parameterList,
            diagnostics,
            allowRef,
            addRefConstModifier,
            lastIndex: parameterList.Count - 1,
            parameterCreationFunc: (Symbol owner, TypeWithAnnotations parameterType,
                                    ParameterSyntax syntax, RefKind refKind,
                                    int ordinal, bool addRefConstModifier, ScopedKind scope) => {
                                        return SourceParameterSymbol.Create(
                                                owner,
                                                parameterType,
                                                syntax,
                                                refKind,
                                                syntax.identifier,
                                                ordinal,
                                                scope
                                            );
                                    });
    }

    internal static bool ReportDefaultParameterErrors(
        Binder binder,
        Symbol owner,
        ParameterSyntax parameterSyntax,
        SourceParameterSymbol parameter,
        BoundExpression defaultExpression,
        BoundExpression convertedExpression,
        BelteDiagnosticQueue diagnostics) {
        var hasErrors = false;

        var parameterType = parameter.type;
        var conversion = binder.conversions.ClassifyImplicitConversionFromExpression(defaultExpression, parameterType);
        var refKind = GetModifiers(parameterSyntax.modifiers, out var refnessKeyword);

        if (refKind == RefKind.Ref) {
            diagnostics.Push(Error.RefDefaultValue(refnessKeyword.location));
            hasErrors = true;
        } else if (!defaultExpression.hasErrors && !IsValidDefaultValue(defaultExpression)) {
            diagnostics.Push(Error.DefaultMustBeConstant(
                parameterSyntax.defaultValue.value.location,
                parameterSyntax.identifier.text
            ));

            hasErrors = true;
        } else if (!conversion.exists) {
            diagnostics.Push(Error.NoCastForDefaultParameter(
                parameterSyntax.identifier.location,
                defaultExpression.type,
                parameterType
            ));

            hasErrors = true;
        } else if (conversion.isBoxing) {
            diagnostics.Push(Error.NotNullRefDefaultParameter(
                parameterSyntax.identifier.location,
                parameterSyntax.identifier.text,
                parameterType
            ));

            hasErrors = true;
        } else if (conversion.isNullable && !defaultExpression.type.IsNullableType()) {
            // We can do:
            // M(int? x = default(int))
            // M(int? x = default(int?))
            // M(MyEnum? e = default(enum))
            // M(MyEnum? e = default(enum?))
            // M(MyStruct? s = default(MyStruct?))
            //
            // but we cannot do:
            //
            // M(MyStruct? s = default(MyStruct))

            // error CS1770:
            // A value of type '{0}' cannot be used as default parameter for nullable parameter '{1}' because '{0}' is not a simple type
            // diagnostics.Add(ErrorCode.ERR_NoConversionForNubDefaultParam, parameterSyntax.Identifier.GetLocation(),
            // (defaultExpression.IsImplicitObjectCreation() ? convertedExpression.Type.StrippedType() : defaultExpression.Type), parameterSyntax.Identifier.ValueText);
            // TODO Consider if we even want this error

            // hasErrors = true;
        }

        if (owner.IsOperator()) {
            diagnostics.Push(Warning.DefaultValueNoEffect(
                parameterSyntax.identifier.location,
                parameterSyntax.identifier.text
            ));
        }

        if (refKind == RefKind.RefConstParameter) {
            diagnostics.Push(Warning.RefConstParameterDefaultValue(
                parameterSyntax.defaultValue.value.location,
                parameterSyntax.identifier.text
            ));
        }

        return hasErrors;
    }

    private static bool IsValidDefaultValue(BoundExpression expression) {
        if (expression.constantValue is not null)
            return true;

        return false;
    }

    internal static RefKind GetModifiers(SyntaxTokenList modifiers, out SyntaxToken refnessKeyword) {
        refnessKeyword = null;

        if (modifiers is null)
            return RefKind.None;

        var refKind = RefKind.None;

        foreach (var modifier in modifiers) {
            switch (modifier.kind) {
                case SyntaxKind.RefKeyword:
                    if (refKind == RefKind.None) {
                        refnessKeyword = modifier;
                        refKind = RefKind.Ref;
                    }

                    break;
                // TODO Consider using readonly keyword here instead
                case SyntaxKind.ConstKeyword:
                    if (refKind == RefKind.Ref && refnessKeyword.GetNextToken() == modifier)
                        refKind = RefKind.RefConstParameter;

                    break;
            }
        }

        return refKind;
    }

    public static void ReportParameterErrors(
        Symbol owner,
        ParameterSyntax syntax,
        int ordinal,
        int lastParameterIndex,
        TypeWithAnnotations typeWithAnnotations,
        RefKind refKind,
        Symbol containingSymbol,
        int firstDefault,
        BelteDiagnosticQueue diagnostics) {
        var parameterIndex = ordinal;
        var isDefault = syntax is ParameterSyntax { defaultValue: { } };

        if (typeWithAnnotations.nullableUnderlyingTypeOrSelf.isStatic) {
            diagnostics.Push(Error.ParameterIsStatic(syntax.type.location, typeWithAnnotations.type));
        } else if (firstDefault != -1 && parameterIndex > firstDefault && !isDefault) {
            var location = syntax.identifier.GetNextToken(includeZeroWidth: true).location;
            diagnostics.Push(Error.DefaultBeforeNoDefault(location));
        }
    }

    private static ImmutableArray<TParameterSymbol> MakeParameters<TParameterSymbol, TOwningSymbol>(
        Binder withTemplateParametersBinder,
        TOwningSymbol owner,
        SeparatedSyntaxList<ParameterSyntax> parametersList,
        BelteDiagnosticQueue diagnostics,
        bool allowRef,
        bool addRefConstModifier,
        int lastIndex,
        Func<TOwningSymbol, TypeWithAnnotations, ParameterSyntax, RefKind, int, bool, ScopedKind, TParameterSymbol> parameterCreationFunc)
        where TParameterSymbol : ParameterSymbol
        where TOwningSymbol : Symbol {

        var parameterIndex = 0;
        var firstDefault = -1;

        var builder = ArrayBuilder<TParameterSymbol>.GetInstance();

        foreach (var parameterSyntax in parametersList) {
            if (parameterIndex > lastIndex) break;

            CheckParameterModifiers(parameterSyntax, diagnostics);

            var refKind = GetModifiers(parameterSyntax.modifiers, out var refnessKeyword);

            if (parameterSyntax is ParameterSyntax concreteParam) {
                if (concreteParam.defaultValue is not null && firstDefault == -1)
                    firstDefault = parameterIndex;
            }

            var parameterType = withTemplateParametersBinder.BindType(parameterSyntax.type, diagnostics);

            if (!allowRef && refKind == RefKind.Ref)
                diagnostics.Push(Error.InvalidRefParameter(refnessKeyword.location));

            var parameter = parameterCreationFunc(
                owner,
                parameterType,
                parameterSyntax,
                refKind,
                parameterIndex,
                addRefConstModifier,
                ScopedKind.None
            );

            ReportParameterErrors(
                owner,
                parameterSyntax,
                parameter.ordinal,
                lastParameterIndex: lastIndex,
                parameter.typeWithAnnotations,
                parameter.refKind,
                parameter.containingSymbol,
                firstDefault,
                diagnostics
            );

            builder.Add(parameter);
            parameterIndex++;
        }

        var parameters = builder.ToImmutableAndFree();

        var methodOwner = owner as MethodSymbol;
        var templateParameters = methodOwner is not null ? methodOwner.templateParameters : default;

        withTemplateParametersBinder.ValidateParameterNameConflicts(
            templateParameters,
            parameters.Cast<TParameterSymbol, ParameterSymbol>(),
            diagnostics
        );

        return parameters;
    }

    internal static void CheckParameterModifiers(ParameterSyntax parameter, BelteDiagnosticQueue diagnostics) {
        var seenRef = false;
        var seenConst = false;

        SyntaxToken previousModifier = null;

        if (parameter.modifiers is null)
            return;

        foreach (var modifier in parameter.modifiers) {
            switch (modifier.kind) {
                case SyntaxKind.RefKeyword:
                    if (seenRef)
                        AddDupParamMod(diagnostics, modifier);
                    else
                        seenRef = true;

                    break;
                case SyntaxKind.ConstKeyword:
                    if (seenConst)
                        AddDupParamMod(diagnostics, modifier);
                    else if (previousModifier?.kind != SyntaxKind.RefKeyword)
                        diagnostics.Push(Error.RefConstWrongOrder(modifier.location));
                    else if (seenRef)
                        seenConst = true;

                    break;
                default:
                    throw ExceptionUtilities.UnexpectedValue(modifier.kind);
            }

            previousModifier = modifier;
        }

        static void AddDupParamMod(BelteDiagnosticQueue diagnostics, SyntaxToken modifier) {
            diagnostics.Push(Error.ModifierAlreadyApplied(modifier.location, modifier));
        }
    }
}
