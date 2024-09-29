
namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// A type symbol with null clarification.
/// </summary>
internal sealed class TypeWithAnnotations {
    internal TypeWithAnnotations(TypeSymbol underlyingType, bool isNullable) {
        type = underlyingType;
        this.isNullable = isNullable;
    }

    internal TypeWithAnnotations(TypeSymbol underlyingType) {
        type = underlyingType;
        isNullable = type.IsNullableType();
    }

    internal TypeSymbol type { get; }

    internal bool isNullable { get; }

    internal TypeOrConstant SubstituteType(TemplateMap templateMap) {
        var typeSymbol = type;
        var newTypeWithModifiers = templateMap.SubstituteType(typeSymbol);

        if (!typeSymbol.IsTypeParameter()) {
            if (typeSymbol.Equals(newTypeWithModifiers.type, TypeCompareKind.ConsiderEverything)) {
                return this;
            } else if ((NullableAnnotation.IsOblivious() || (typeSymbol.IsNullableType() && NullableAnnotation.IsAnnotated())) &&
                  newCustomModifiers.IsEmpty) {
                return newTypeWithModifiers;
            }

            return Create(newTypeWithModifiers.Type, NullableAnnotation, newCustomModifiers);
        }

        if (newTypeWithModifiers.Is((TypeParameterSymbol)typeSymbol) &&
            newCustomModifiers == CustomModifiers) {
            return this; // substitution had no effect on the type or modifiers
        } else if (Is((TypeParameterSymbol)typeSymbol) && newTypeWithModifiers.NullableAnnotation != NullableAnnotation.Ignored) {
            return newTypeWithModifiers;
        }

        if (newTypeWithModifiers.Type is PlaceholderTypeArgumentSymbol) {
            return newTypeWithModifiers;
        }

        NullableAnnotation newAnnotation;
        Debug.Assert(newTypeWithModifiers.Type is not IndexedTypeParameterSymbol || newTypeWithModifiers.NullableAnnotation == NullableAnnotation.Ignored);

        if (NullableAnnotation.IsAnnotated() || newTypeWithModifiers.NullableAnnotation.IsAnnotated()) {
            newAnnotation = NullableAnnotation.Annotated;
        } else if (newTypeWithModifiers.NullableAnnotation == NullableAnnotation.Ignored) {
            newAnnotation = NullableAnnotation;
        } else if (NullableAnnotation != NullableAnnotation.Oblivious) {
            Debug.Assert(NullableAnnotation == NullableAnnotation.NotAnnotated);
            if (newTypeWithModifiers.NullableAnnotation == NullableAnnotation.Oblivious) {
                // When the type parameter disallows a nullable reference type as a type argument (i.e. IsNotNullable),
                // we want to drop any Oblivious annotation from the substituted type and use NotAnnotated instead,
                // to reflect the "stronger" claim being made by the type parameter.
                var typeParameter = (TypeParameterSymbol)typeSymbol;
                if (typeParameter.CalculateIsNotNullableFromNonTypeConstraints() == true) {
                    newAnnotation = NullableAnnotation.NotAnnotated;
                } else {
                    // We won't know the substituted type's nullable annotation
                    // until we bind type constraints on the type parameter.
                    // We need to delay doing this to avoid a cycle.
                    Debug.Assert((object)newTypeWithModifiers.DefaultType == newTypeWithModifiers.Type);
                    return CreateLazySubstitutedType(newTypeWithModifiers.DefaultType, newCustomModifiers.Concat(newTypeWithModifiers.CustomModifiers), typeParameter);
                }
            } else {
                Debug.Assert(newTypeWithModifiers.NullableAnnotation is NullableAnnotation.NotAnnotated);
                newAnnotation = NullableAnnotation.NotAnnotated;
            }
        } else if (newTypeWithModifiers.NullableAnnotation != NullableAnnotation.Oblivious) {
            newAnnotation = newTypeWithModifiers.NullableAnnotation;
        } else {
            Debug.Assert(NullableAnnotation.IsOblivious());
            Debug.Assert(newTypeWithModifiers.NullableAnnotation.IsOblivious());
            newAnnotation = NullableAnnotation;
        }

        return CreateNonLazyType(
            newTypeWithModifiers.Type,
            newAnnotation,
            newCustomModifiers.Concat(newTypeWithModifiers.CustomModifiers));
    }

    public bool Equals(TypeWithAnnotations other) {
        return Equals(other, TypeCompareKind.ConsiderEverything);
    }

    public bool Equals(TypeWithAnnotations other, TypeCompareKind compareKind) {
        if (compareKind == TypeCompareKind.ConsiderEverything)
            return isNullable == other.isNullable && type == other.type;

        if (compareKind == TypeCompareKind.IgnoreNullability)
            return type == other.type;

        return false;
    }
}
