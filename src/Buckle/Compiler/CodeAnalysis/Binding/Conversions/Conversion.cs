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

        if (source == target)
            return Identity;

        var sourceIsNullable = source.specialType == SpecialType.Nullable;
        var targetIsNullable = target.specialType == SpecialType.Nullable;

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
}
