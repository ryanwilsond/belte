using System;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Symbols;

internal static class TypeWithAnnotationsExtensions {
    internal static TypeSymbol VisitType<T>(
        this TypeWithAnnotations typeWithAnnotationsOpt,
        TypeSymbol type,
        Func<TypeWithAnnotations, T, bool, bool>? typeWithAnnotationsPredicate,
        Func<TypeSymbol, T, bool, bool>? typePredicate,
        T arg,
        bool canDigThroughNullable = false) {
        while (true) {
            var current = type ?? typeWithAnnotationsOpt.type;
            var isNestedNamedType = false;

            if (current.typeKind is TypeKind.Class or TypeKind.Struct) {
                var containingType = current.containingType;

                if (containingType is not null) {
                    isNestedNamedType = true;
                    var result = VisitType(
                        default,
                        containingType,
                        typeWithAnnotationsPredicate,
                        typePredicate,
                        arg,
                        canDigThroughNullable
                    );

                    if (result is not null)
                        return result;
                }
            }

            if (typeWithAnnotationsOpt?.hasType == true && typeWithAnnotationsPredicate is not null) {
                if (typeWithAnnotationsPredicate(typeWithAnnotationsOpt, arg, isNestedNamedType))
                    return current;
            } else if (typePredicate is not null) {
                if (typePredicate(current, arg, isNestedNamedType))
                    return current;
            }

            TypeWithAnnotations next;

            switch (current.typeKind) {
                case TypeKind.Primitive:
                case TypeKind.TemplateParameter:
                    return null;
                case TypeKind.Error:
                case TypeKind.Class:
                case TypeKind.Struct:
                    var templateArguments = ((NamedTypeSymbol)current).templateArguments;

                    if (templateArguments.IsEmpty)
                        return null;

                    int i;
                    for (i = 0; i < templateArguments.Length - 1; i++) {
                        (var nextTypeWithAnnotations, var nextType) = GetNextIterationElements(
                            templateArguments[i].type,
                            canDigThroughNullable
                        );

                        var result = VisitType(
                            typeWithAnnotationsOpt: nextTypeWithAnnotations,
                            type: nextType,
                            typeWithAnnotationsPredicate,
                            typePredicate,
                            arg,
                            canDigThroughNullable
                        );

                        if (result is not null)
                            return result;
                    }

                    next = templateArguments[i].type;
                    break;

                case TypeKind.Array:
                    next = ((ArrayTypeSymbol)current).elementTypeWithAnnotations;
                    break;
                default:
                    throw ExceptionUtilities.UnexpectedValue(current.typeKind);
            }

            typeWithAnnotationsOpt = canDigThroughNullable ? null : next;
            type = canDigThroughNullable ? next.nullableUnderlyingTypeOrSelf : null;
        }

        static (TypeWithAnnotations, TypeSymbol) GetNextIterationElements(
            TypeWithAnnotations type,
            bool canDigThroughNullable) {
            return canDigThroughNullable
                ? (null, type.nullableUnderlyingTypeOrSelf)
                : (type, null);
        }
    }
}
