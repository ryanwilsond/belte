using System.Collections.Generic;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;
using Buckle.Utilities;

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

    internal static bool IsValidSwitchCaseLabelConstant(ConstantValue constant) {
        if (ConstantValue.IsNull(constant))
            return true;

        switch (constant.specialType) {
            case SpecialType.Int8:
            case SpecialType.UInt8:
            case SpecialType.Int16:
            case SpecialType.UInt16:
            case SpecialType.Int32:
            case SpecialType.UInt32:
            case SpecialType.Int64:
            case SpecialType.UInt64:
            case SpecialType.Char:
            case SpecialType.Bool:
            case SpecialType.String:
                return true;
            default:
                return false;
        }
    }

    internal static int CompareSwitchCaseLabelConstants(ConstantValue first, ConstantValue second) {
        if (ConstantValue.IsNull(first))
            return ConstantValue.IsNull(second) ? 0 : -1;
        else if (ConstantValue.IsNull(second))
            return 1;

        switch (first.specialType) {
            case SpecialType.Int8:
                return ((sbyte)first.value).CompareTo((sbyte)second.value);
            case SpecialType.Int16:
                return ((short)first.value).CompareTo((short)second.value);
            case SpecialType.Int32:
                return ((int)first.value).CompareTo((int)second.value);
            case SpecialType.Int64:
            case SpecialType.Int:
                return ((long)first.value).CompareTo((long)second.value);
            case SpecialType.Bool:
                return ((bool)first.value).CompareTo((bool)second.value);
            case SpecialType.UInt8:
                return ((byte)first.value).CompareTo((byte)second.value);
            case SpecialType.UInt16:
                return ((ushort)first.value).CompareTo((ushort)second.value);
            case SpecialType.UInt32:
                return ((uint)first.value).CompareTo((uint)second.value);
            case SpecialType.UInt64:
                return ((ulong)first.value).CompareTo((ulong)second.value);
            case SpecialType.Char:
                return ((char)first.value).CompareTo((char)second.value);
            case SpecialType.String:
                return string.CompareOrdinal((string)first.value, (string)second.value);
            default:
                throw ExceptionUtilities.UnexpectedValue(first.specialType);
        }
    }
}
