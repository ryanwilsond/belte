using System.Collections.Generic;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Symbols;

internal static class ConstantValueHelpers {
    internal static ConstantValue EvaluateFieldConstant(
        SourceFieldSymbol symbol,
        EqualsValueClauseSyntax equalsValueClause,
        HashSet<SourceFieldSymbolWithSyntaxReference> dependencies,
        BelteDiagnosticQueue diagnostics) {
        var compilation = symbol.declaringCompilation;
        var binderFactory = compilation.GetBinderFactory(equalsValueClause.syntaxTree);
        var binder = binderFactory.GetBinder(equalsValueClause);

        var inProgressBinder = new ConstantFieldsInProgressBinder(new ConstantFieldsInProgress(symbol, dependencies), binder);
        var boundValue = BindFieldOrEnumInitializer(inProgressBinder, symbol, equalsValueClause, diagnostics);

        var value = GetAndValidateConstantValue(
            boundValue.value,
            symbol,
            symbol.type,
            equalsValueClause.value,
            diagnostics
        );

        return value;
    }

    internal static ConstantValue GetAndValidateConstantValue(
        BoundExpression boundValue,
        Symbol thisSymbol,
        TypeSymbol typeSymbol,
        SyntaxNode initValueNode,
        BelteDiagnosticQueue diagnostics) {
        ConstantValue value = null;

        if (boundValue is not BoundErrorExpression) {
            if (typeSymbol.typeKind == TypeKind.TemplateParameter) {
                // diagnostics.Add(ErrorCode.ERR_InvalidConstantDeclarationType, initValueNode.Location, thisSymbol, typeSymbol);
                // TODO implement this error
            } else {
                var unconvertedBoundValue = boundValue;
                var constantValue = boundValue.constantValue;
                var unconvertedConstantValue = unconvertedBoundValue.constantValue;

                if (ConstantValue.IsNotNull(unconvertedConstantValue) && typeSymbol.isObjectType) {
                    // diagnostics.Add(ErrorCode.ERR_NotNullConstRefField, initValueNode.Location, thisSymbol, typeSymbol);
                    // TODO Confirm this error should be raised
                    constantValue ??= unconvertedConstantValue;
                }

                if (constantValue is not null)
                    value = constantValue;
                else
                    diagnostics.Push(Error.ConstantExpected(initValueNode.location));
            }
        }

        return value;
    }

    private static BoundFieldEqualsValue BindFieldOrEnumInitializer(
        Binder binder,
        FieldSymbol fieldSymbol,
        EqualsValueClauseSyntax initializer,
        BelteDiagnosticQueue diagnostics) {
        var enumConstant = fieldSymbol as SourceEnumConstantSymbol;
        Binder collisionDetector = new LocalScopeBinder(binder);
        collisionDetector = new ExecutableCodeBinder(initializer, fieldSymbol, collisionDetector);
        BoundFieldEqualsValue result;

        if (enumConstant is not null)
            result = collisionDetector.BindEnumConstantInitializer(enumConstant, initializer, diagnostics);
        else
            result = collisionDetector.BindFieldInitializer(fieldSymbol, initializer, diagnostics);

        return result;
    }
}
