
namespace Buckle.CodeAnalysis.Symbols;

internal static class TypeUnification {
    public static bool CanUnify(TypeSymbol t1, TypeSymbol t2) {
        if (TypeSymbol.Equals(t1, t2, TypeCompareKind.CLRSignatureCompareOptions))
            return true;

        MutableTemplateMap substitution = null;
        var result = CanUnifyHelper(t1, t2, ref substitution);
        return result;
    }

    private static bool CanUnifyHelper(TypeSymbol t1, TypeSymbol t2, ref MutableTemplateMap substitution) {
        return CanUnifyHelper(new TypeWithAnnotations(t1), new TypeWithAnnotations(t2), ref substitution);
    }

    private static bool CanUnifyHelper(
        TypeWithAnnotations t1,
        TypeWithAnnotations t2,
        ref MutableTemplateMap substitution) {
        if (!t1.hasType || !t2.hasType)
            return t1.IsSameAs(t2);

        if (substitution is not null) {
            t1 = t1.SubstituteType(substitution).type;
            t2 = t2.SubstituteType(substitution).type;
        }

        if (TypeSymbol.Equals(t1.type, t2.type, TypeCompareKind.CLRSignatureCompareOptions))
            return true;

        if (!t1.type.IsTemplateParameter() && t2.type.IsTemplateParameter())
            (t2, t1) = (t1, t2);

        switch (t1.type.kind) {
            case SymbolKind.ArrayType: {
                    if (t2.typeKind != t1.typeKind)
                        return false;

                    var at1 = (ArrayTypeSymbol)t1.type;
                    var at2 = (ArrayTypeSymbol)t2.type;

                    if (!at1.HasSameShapeAs(at2))
                        return false;

                    return CanUnifyHelper(
                        at1.elementTypeWithAnnotations,
                        at2.elementTypeWithAnnotations,
                        ref substitution
                    );
                }
            case SymbolKind.PointerType: {
                    if (t2.typeKind != t1.typeKind)
                        return false;

                    var pt1 = (PointerTypeSymbol)t1.type;
                    var pt2 = (PointerTypeSymbol)t2.type;

                    return CanUnifyHelper(
                        pt1.pointedAtTypeWithAnnotations,
                        pt2.pointedAtTypeWithAnnotations,
                        ref substitution
                    );
                }
            case SymbolKind.NamedType:
            case SymbolKind.ErrorType: {
                    if (t2.typeKind != t1.typeKind)
                        return false;

                    var nt1 = (NamedTypeSymbol)t1.type;
                    var nt2 = (NamedTypeSymbol)t2.type;

                    if (!nt1.isTemplateType || !nt2.isTemplateType)
                        return false;

                    var arity = nt1.arity;

                    if (nt2.arity != arity ||
                        !TypeSymbol.Equals(
                            nt2.originalDefinition,
                            nt1.originalDefinition,
                            TypeCompareKind.ConsiderEverything)) {
                        return false;
                    }

                    var nt1Arguments = nt1.templateArguments;
                    var nt2Arguments = nt2.templateArguments;

                    for (var i = 0; i < arity; i++) {
                        if (!CanUnifyHelper(
                                nt1Arguments[i].type,
                                nt2Arguments[i].type,
                                ref substitution)) {
                            return false;
                        }
                    }

                    return nt1.containingType is null ||
                        CanUnifyHelper(nt1.containingType, nt2.containingType, ref substitution);
                }
            case SymbolKind.TemplateParameter: {
                    if (t2.type.IsPointerOrFunctionPointer() || t2.IsVoidType())
                        return false;

                    var tp1 = (TemplateParameterSymbol)t1.type;

                    if (Contains(t2.type, tp1))
                        return false;

                    AddSubstitution(ref substitution, tp1, t2);
                    return true;
                }
            default:
                return false;
        }
    }

    private static void AddSubstitution(
        ref MutableTemplateMap substitution,
        TemplateParameterSymbol tp1,
        TypeWithAnnotations t2) {
        substitution ??= new MutableTemplateMap();
        substitution.Add(tp1, new TypeOrConstant(t2));
    }

    private static bool Contains(TypeSymbol type, TemplateParameterSymbol typeParam) {
        switch (type.kind) {
            case SymbolKind.ArrayType:
                return Contains(((ArrayTypeSymbol)type).elementType, typeParam);
            case SymbolKind.PointerType:
                return Contains(((PointerTypeSymbol)type).pointedAtType, typeParam);
            case SymbolKind.NamedType:
            case SymbolKind.ErrorType: {
                    var namedType = (NamedTypeSymbol)type;

                    while (namedType is not null) {
                        var typeParts = namedType.isTupleType
                            ? namedType.tupleElementTypes
                            : namedType.templateArguments;

                        foreach (var typePart in typeParts) {
                            if (Contains(typePart.type.type, typeParam))
                                return true;
                        }

                        namedType = namedType.containingType;
                    }

                    return false;
                }
            case SymbolKind.TemplateParameter:
                return TypeSymbol.Equals(type, typeParam, TypeCompareKind.ConsiderEverything);
            default:
                return false;
        }
    }
}
