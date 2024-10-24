using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A cast from any <see cref="BoundType" /> to any <see cref="BoundType" /> (can be the same).
/// </summary>
internal sealed partial class Conversion {
    internal static readonly Conversion None = new Conversion(ConversionKind.None);

    internal static readonly Conversion Identity = new Conversion(ConversionKind.Identity);

    internal static readonly Conversion Implicit = new Conversion(ConversionKind.Implicit);

    internal static readonly Conversion ImplicitNullable = new Conversion(ConversionKind.ImplicitNullable);

    internal static readonly Conversion ImplicitReference = new Conversion(ConversionKind.ImplicitReference);

    internal static readonly Conversion Boxing = new Conversion(ConversionKind.Boxing);

    internal static readonly Conversion BoxingImplicitNullable = new Conversion(ConversionKind.BoxingImplicitNullable);

    internal static readonly Conversion BoxingExplicitNullable = new Conversion(ConversionKind.BoxingExplicitNullable);

    internal static readonly Conversion AnyBoxing = new Conversion(ConversionKind.AnyBoxing);

    internal static readonly Conversion AnyBoxingImplicitNullable = new Conversion(ConversionKind.AnyBoxingImplicitNullable);

    internal static readonly Conversion AnyBoxingExplicitNullable = new Conversion(ConversionKind.AnyBoxingExplicitNullable);

    internal static readonly Conversion Explicit = new Conversion(ConversionKind.Explicit);

    internal static readonly Conversion ExplicitNullable = new Conversion(ConversionKind.ExplicitNullable);

    internal static readonly Conversion ExplicitReference = new Conversion(ConversionKind.ExplicitReference);

    internal static readonly Conversion Unboxing = new Conversion(ConversionKind.Unboxing);

    internal static readonly Conversion UnboxingImplicitNullable = new Conversion(ConversionKind.UnboxingImplicitNullable);

    internal static readonly Conversion UnboxingExplicitNullable = new Conversion(ConversionKind.UnboxingExplicitNullable);

    internal static readonly Conversion AnyUnboxing = new Conversion(ConversionKind.AnyUnboxing);

    internal static readonly Conversion AnyUnboxingImplicitNullable = new Conversion(ConversionKind.AnyUnboxingImplicitNullable);

    internal static readonly Conversion AnyUnboxingExplicitNullable = new Conversion(ConversionKind.AnyUnboxingExplicitNullable);

    private Conversion(ConversionKind castKind) {
        kind = castKind;
    }

    internal ConversionKind kind { get; }

    /// <summary>
    /// If a cast exists (otherwise you cant go from one type to the other).
    /// </summary>
    internal bool exists => kind != ConversionKind.None;

    /// <summary>
    /// If the <see cref="Conversion" /> is an identity cast.
    /// </summary>
    internal bool isIdentity => kind == ConversionKind.Identity;

    /// <summary>
    /// If the <see cref="Conversion" /> is an implicit cast.
    /// </summary>
    internal bool isImplicit => kind.IsImplicitCast();

    /// <summary>
    /// If the <see cref="Conversion" /> is an explicit cast.
    /// A <see cref="Conversion" /> cannot be implicit and explicit.
    /// </summary>
    internal bool isExplicit => exists && !isImplicit;

    internal bool isNullable => kind.IsNullableCast();

    internal bool isReference => kind is ConversionKind.ImplicitReference or ConversionKind.ExplicitReference;

    internal bool isBoxing => kind.IsBoxingCast();

    internal bool isUnboxing => kind.IsUnboxingCast();

    /// <summary>
    /// Classify what type of <see cref="Conversion" /> is required to go from one type to the other.
    /// </summary>
    internal static Conversion Classify(TypeSymbol source, TypeSymbol target) {
        if (source.typeKind == TypeKind.Primitive && target.typeKind == TypeKind.Primitive)
            return new Conversion(EasyOut.Classify(source, target));

        if (source.typeKind == TypeKind.Primitive || target.typeKind == TypeKind.Primitive)
            return None;

        if (source.Equals(target))
            return Identity;

        var sourceIsNullable = source.IsNullableType();
        var targetIsNullable = target.IsNullableType();

        if (source.typeWithAnnotations.underlyingType == target.typeWithAnnotations.underlyingType) {
            if (sourceIsNullable && targetIsNullable)
                return Identity;
            else if (sourceIsNullable)
                return ExplicitNullable;
            else
                return ImplicitNullable;
        }

        if (source.typeWithAnnotations.underlyingType.InheritsFrom(target.typeWithAnnotations.underlyingType)) {
            if (sourceIsNullable && targetIsNullable)
                return ImplicitNullable;
            else if (sourceIsNullable)
                return ExplicitNullable;
            else
                return ImplicitNullable;
        }

        if (target.typeWithAnnotations.underlyingType.InheritsFrom(source.typeWithAnnotations.underlyingType)) {
            if (sourceIsNullable || targetIsNullable)
                return ExplicitNullable;
            else
                return Explicit;
        }

        return None;
    }

    internal static bool IsBaseClass(TypeSymbol derivedType, TypeSymbol baseType) {
        if (!baseType.IsClassType())
            return false;

        for (TypeSymbol b = derivedType.baseType; (object)b is not null; b = b.baseType) {
            if (HasIdentityConversionInternal(b, baseType))
                return true;
        }

        return false;
    }

    internal static bool HasIdentityOrImplicitConversion(TypeSymbol source, TypeSymbol destination) {
        if (HasIdentityConversionInternal(source, destination))
            return true;

        return HasImplicitConversion(source, destination);
    }

    internal static bool HasImplicitConversion(TypeSymbol source, TypeSymbol destination) {
        if (source.IsErrorType())
            return false;

        if (destination.specialType == SpecialType.Object)
            return true;

        switch (source.typeKind) {
            case TypeKind.Class:
                return IsBaseClass(source, destination);
            case TypeKind.TemplateParameter:
                return HasImplicitTypeParameterConversion((TemplateParameterSymbol)source, destination);
            case TypeKind.Array:
                return HasImplicitConversionFromArray(source, destination);
        }

        return false;
    }

    internal static bool HasTopLevelNullabilityImplicitConversion(
        TypeWithAnnotations source,
        TypeWithAnnotations destination) {
        if (destination.isNullable)
            return true;

        if (IsPossiblyNullableTypeTypeParameter(source) && !IsPossiblyNullableTypeTypeParameter(destination))
            return false;

        return !source.isNullable;
    }

    internal static bool HasBoxingConversion(TypeSymbol source, TypeSymbol destination) {
        if ((source.typeKind == TypeKind.TemplateParameter) &&
            HasImplicitBoxingTemplateParameterConversion((TemplateParameterSymbol)source, destination)) {
            return true;
        }

        // The rest of the boxing conversions only operate when going from a specific primitive type to the `any` type
        if (!source.isPrimitiveType || destination.originalDefinition.specialType != SpecialType.Any)
            return false;

        if (source.IsNullableType())
            return HasBoxingConversion(source.GetNullableUnderlyingType(), destination);

        // TODO Return false?
        return true;
    }

    private static bool HasImplicitBoxingTemplateParameterConversion(
        TemplateParameterSymbol source,
        TypeSymbol destination) {
        // TODO Does this conflict with the notion that "boxing" conversions have a destination of `any`?
        if (source.isObjectType)
            return false;

        if (HasImplicitEffectiveBaseConversion(source, destination))
            return true;

        return false;
    }

    private static bool IsPossiblyNullableTypeTypeParameter(TypeWithAnnotations typeWithAnnotations) {
        var type = typeWithAnnotations.type;
        return type is not null &&
            (type.IsPossiblyNullableTypeTemplateParameter() || type.IsNullableTypeOrTypeParameter());
    }

    private static bool HasImplicitConversion(TypeWithAnnotations source, TypeWithAnnotations destination) {
        if (!HasTopLevelNullabilityImplicitConversion(source, destination))
            return false;

        if (source.isNullable != destination.isNullable &&
            HasIdentityConversionInternal(source.type, destination.type, includeNullability: true)) {
            return true;
        }

        return HasImplicitConversion(source.type, destination.type);
    }

    private static bool HasImplicitTypeParameterConversion(
        TemplateParameterSymbol source,
        TypeSymbol destination) {
        if (HasImplicitEffectiveBaseConversion(source, destination))
            return true;

        return false;
    }

    private static bool HasImplicitConversionFromArray(TypeSymbol source, TypeSymbol destination) {
        if (source is not ArrayTypeSymbol)
            return false;

        if (HasCovariantArrayConversion(source, destination))
            return true;

        if (destination.GetSpecialTypeSafe() == SpecialType.Array)
            return true;

        return false;
    }

    private static bool HasCovariantArrayConversion(TypeSymbol source, TypeSymbol destination) {
        if (source is not ArrayTypeSymbol s || destination is not ArrayTypeSymbol d)
            return false;

        if (!s.HasSameShapeAs(d))
            return false;

        return HasImplicitConversion(s.elementTypeWithAnnotations, d.elementTypeWithAnnotations);
    }

    private static bool HasImplicitEffectiveBaseConversion(TemplateParameterSymbol source, TypeSymbol destination) {
        var effectiveBaseClass = source.effectiveBaseClass;
        return HasIdentityConversionInternal(effectiveBaseClass, destination) ||
            IsBaseClass(effectiveBaseClass, destination);
    }

    private static bool HasIdentityConversionInternal(TypeSymbol type1, TypeSymbol type2) {
        return HasIdentityConversionInternal(type1, type2, includeNullability: false);
    }

    private static bool HasIdentityConversionInternal(TypeSymbol type1, TypeSymbol type2, bool includeNullability) {
        var compareKind = includeNullability
            ? TypeCompareKind.AllIgnoreOptions & ~TypeCompareKind.IgnoreNullability
            : TypeCompareKind.AllIgnoreOptions;

        return type1.Equals(type2, compareKind);
    }
}
