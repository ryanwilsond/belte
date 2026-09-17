using System;
using System.Collections.Immutable;
using System.Linq;
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
        bool addRefConstModifier,
        bool allowConst) {
        return MakeParameters(
            withTemplateParametersBinder,
            owner,
            parameterList,
            diagnostics,
            allowRef,
            addRefConstModifier,
            allowConst,
            lastIndex: parameterList.Count - 1,
            parameterCreationFunc: (Symbol owner, TypeWithAnnotations parameterType,
                                    ParameterSyntax syntax, RefKind refKind, bool isConst,
                                    int ordinal, bool addRefConstModifier, ScopedKind scope) => {
                                        if (parameterType.IsVoidType())
                                            diagnostics.Push(Error.VoidUsedAsType(syntax.type.location));

                                        return SourceParameterSymbol.Create(
                                                owner,
                                                parameterType,
                                                syntax,
                                                refKind,
                                                isConst,
                                                syntax.identifier.valueText,
                                                ordinal,
                                                scope
                                            );
                                    });
    }

    internal static ImmutableArray<FunctionPointerParameterSymbol> MakeFunctionPointerParameters(
        Binder binder,
        FunctionPointerMethodSymbol owner,
        SeparatedSyntaxList<FunctionPointerParameterSyntax> parametersList,
        BelteDiagnosticQueue diagnostics) {
        var names = parametersList.Select(p => p.identifier?.valueText);

        return MakeParameters(
            binder,
            owner,
            parametersList,
            diagnostics,
            allowRef: true,
            addRefConstModifier: true,
            allowConst: true,
            parametersList.Count - 1,
            parameterCreationFunc: (FunctionPointerMethodSymbol owner, TypeWithAnnotations parameterType,
                                    FunctionPointerParameterSyntax syntax, RefKind refKind, bool isConst,
                                    int ordinal, bool addRefReadOnlyModifier, ScopedKind scope) => {
                                        if (parameterType.IsVoidType())
                                            diagnostics.Push(Error.VoidUsedAsType(syntax.type.location));

                                        return new FunctionPointerParameterSymbol(
                                            parameterType,
                                            refKind,
                                            isConst,
                                            syntax.identifier?.valueText ?? MakeDefaultName(ordinal, names),
                                            ordinal,
                                            owner
                                        );
                                    },
            parsingFunctionPointer: true
        );
    }

    private static string MakeDefaultName(int ordinal, System.Collections.Generic.IEnumerable<string> names) {
        string name;
        var num = ordinal;

        do {
            name = $"p{++num}";
        } while (names.Contains(name));

        return name;
    }

    internal static ImmutableArray<FunctionParameterSymbol> MakeFunctionParameters(
        Binder binder,
        FunctionMethodSymbol owner,
        SeparatedSyntaxList<FunctionPointerParameterSyntax> parametersList,
        BelteDiagnosticQueue diagnostics) {
        var names = parametersList.Select(p => p.identifier?.valueText);

        return MakeParameters(
            binder,
            owner,
            parametersList,
            diagnostics,
            allowRef: true,
            addRefConstModifier: true,
            allowConst: true,
            parametersList.Count - 1,
            parameterCreationFunc: (FunctionMethodSymbol owner, TypeWithAnnotations parameterType,
                                    FunctionPointerParameterSyntax syntax, RefKind refKind, bool isConst,
                                    int ordinal, bool addRefReadOnlyModifier, ScopedKind scope) => {
                                        if (parameterType.IsVoidType())
                                            diagnostics.Push(Error.VoidUsedAsType(syntax.type.location));

                                        return new FunctionParameterSymbol(
                                            parameterType,
                                            refKind,
                                            isConst,
                                            syntax.identifier?.valueText ?? MakeDefaultName(ordinal, names),
                                            ordinal,
                                            owner
                                        );
                                    },
            parsingFunctionPointer: true
        );
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
        var refKind = GetModifiers(parameterSyntax.modifiers, out var refnessKeyword, out var isConst);

        if (refKind is not RefKind.None and not RefKind.Out) {
            diagnostics.Push(Error.RefDefaultValue(refnessKeyword.location));
            hasErrors = true;
        } else if (!defaultExpression.hasAnyErrors && !IsValidDefaultValue(defaultExpression)) {
            diagnostics.Push(Error.DefaultMustBeConstant(
                parameterSyntax.defaultValue.value.location,
                parameterSyntax.identifier.valueText
            ));

            hasErrors = true;
        } else if (!conversion.exists) {
            diagnostics.Push(Error.NoCastForDefaultParameter(
                parameterSyntax.identifier.location,
                defaultExpression.Type(),
                parameterType
            ));

            hasErrors = true;
        } else if (conversion.isBoxing) {
            diagnostics.Push(Error.NotNullRefDefaultParameter(
                parameterSyntax.identifier.location,
                parameterSyntax.identifier.valueText,
                parameterType
            ));

            hasErrors = true;
        } else if (conversion.isNullable && !defaultExpression.Type().IsNullableType()) {
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
                parameterSyntax.identifier.valueText
            ));
        }

        return hasErrors;
    }

    private static bool IsValidDefaultValue(BoundExpression expression) {
        if (expression.constantValue is not null)
            return true;

        switch (expression.kind) {
            case BoundKind.DefaultLiteral:
            case BoundKind.DefaultExpression:
                return true;
            default:
                return false;
        }
    }

    internal static RefKind GetModifiers(SyntaxTokenList modifiers, out SyntaxToken refnessKeyword, out bool isConst) {
        refnessKeyword = null;
        isConst = false;

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
                case SyntaxKind.OutKeyword:
                    if (refKind == RefKind.None) {
                        refnessKeyword = modifier;
                        refKind = RefKind.Out;
                    }

                    break;
                case SyntaxKind.ConstKeyword:
                    if (refKind == RefKind.Ref && refnessKeyword.GetNextToken() == modifier)
                        refKind = RefKind.RefConst;
                    else
                        isConst = true;

                    break;
                case SyntaxKind.FinalKeyword:
                    if (refKind == RefKind.Ref && refnessKeyword.GetNextToken() == modifier)
                        refKind = RefKind.RefFinal;

                    break;
            }
        }

        return refKind;
    }

    public static void ReportParameterErrors(
        Symbol owner,
        BaseParameterSyntax syntax,
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
            var location = ((ParameterSyntax)syntax).identifier.GetNextToken(includeZeroWidth: true).location;
            diagnostics.Push(Error.DefaultBeforeNoDefault(location));
        }
    }

    private static ImmutableArray<TParameterSymbol> MakeParameters<TParameterSyntax, TParameterSymbol, TOwningSymbol>(
        Binder withTemplateParametersBinder,
        TOwningSymbol owner,
        SeparatedSyntaxList<TParameterSyntax> parametersList,
        BelteDiagnosticQueue diagnostics,
        bool allowRef,
        bool addRefConstModifier,
        bool allowConst,
        int lastIndex,
        Func<TOwningSymbol, TypeWithAnnotations, TParameterSyntax, RefKind, bool, int, bool, ScopedKind, TParameterSymbol> parameterCreationFunc,
        bool parsingFunctionPointer = false)
        where TParameterSyntax : BaseParameterSyntax
        where TParameterSymbol : ParameterSymbol
        where TOwningSymbol : Symbol {

        var parameterIndex = 0;
        var firstDefault = -1;

        var builder = ArrayBuilder<TParameterSymbol>.GetInstance();

        foreach (var parameterSyntax in parametersList) {
            if (parameterIndex > lastIndex) break;

            CheckParameterModifiers(parameterSyntax, allowConst, diagnostics);

            var refKind = GetModifiers(parameterSyntax.modifiers, out var refnessKeyword, out var isConst);

            if (parameterSyntax is ParameterSyntax concreteParam) {
                if (concreteParam.defaultValue is not null && firstDefault == -1)
                    firstDefault = parameterIndex;
            }

            var parameterType = withTemplateParametersBinder.BindType(parameterSyntax.type, diagnostics);

            if (!allowRef && refKind is RefKind.Ref or RefKind.Out)
                diagnostics.Push(Error.InvalidRefParameter(refnessKeyword.location));

            // TODO This is what we do instead of definite assignment analysis (this is easier)
            // TODO BUT this does restrict functionality so we want to change this eventually
            if (refKind is RefKind.Out && !parameterType.type.HasDefaultValue())
                diagnostics.Push(Error.OutNoDefaultValue(parameterSyntax.type.location, parameterType.type));

            var parameter = parameterCreationFunc(
                owner,
                parameterType,
                parameterSyntax,
                refKind,
                isConst,
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

    internal static void CheckParameterModifiers(
        BaseParameterSyntax parameter,
        bool allowConst,
        BelteDiagnosticQueue diagnostics) {
        var seenRef = false;
        var seenOut = false;
        var seenConst = false;

        SyntaxToken previousModifier = null;

        if (parameter.modifiers is null)
            return;

        foreach (var modifier in parameter.modifiers) {
            switch (modifier.kind) {
                case SyntaxKind.RefKeyword:
                    if (seenRef)
                        AddDupParamMod(diagnostics, modifier);
                    else if (seenOut)
                        AddBadMod(diagnostics, modifier, SyntaxKind.OutKeyword);
                    else
                        seenRef = true;

                    break;
                case SyntaxKind.OutKeyword:
                    if (seenOut)
                        AddDupParamMod(diagnostics, modifier);
                    else if (seenRef)
                        AddBadMod(diagnostics, modifier, SyntaxKind.RefKeyword);
                    else
                        seenOut = true;

                    break;
                case SyntaxKind.ConstKeyword:
                    if (seenConst && previousModifier?.kind != SyntaxKind.RefKeyword)
                        AddDupParamMod(diagnostics, modifier);
                    else if (!allowConst && previousModifier?.kind != SyntaxKind.RefKeyword)
                        // TODO Add this check to template parameters
                        diagnostics.Push(Error.RefConstWrongOrder(modifier.location));
                    else
                        seenConst = true;

                    break;
                case SyntaxKind.FinalKeyword:
                    if (previousModifier?.kind != SyntaxKind.RefKeyword)
                        diagnostics.Push(Error.RefFinalWrongOrder(modifier.location));

                    break;
                default:
                    throw ExceptionUtilities.UnexpectedValue(modifier.kind);
            }

            previousModifier = modifier;
        }

        static void AddDupParamMod(BelteDiagnosticQueue diagnostics, SyntaxToken modifier) {
            diagnostics.Push(Error.ModifierAlreadyApplied(modifier.location, modifier));
        }

        static void AddBadMod(BelteDiagnosticQueue diagnostics, SyntaxToken modifier, SyntaxKind otherModifierKind) {
            diagnostics.Push(Error.ConflictingModifiers(
                modifier.location,
                SyntaxFacts.GetText(modifier.kind),
                SyntaxFacts.GetText(otherModifierKind)
            ));
        }
    }
}
