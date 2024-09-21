using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A cast from any <see cref="BoundType" /> to any <see cref="BoundType" /> (can be the same).
/// </summary>
internal sealed partial class Cast {
    internal static readonly Cast None = new Cast(CastKind.None);

    internal static readonly Cast Identity = new Cast(CastKind.Identity);

    internal static readonly Cast Implicit = new Cast(CastKind.Implicit);

    internal static readonly Cast ImplicitNullable = new Cast(CastKind.ImplicitNullable);

    internal static readonly Cast ImplicitReference = new Cast(CastKind.ImplicitReference);

    internal static readonly Cast Boxing = new Cast(CastKind.Boxing);

    internal static readonly Cast BoxingImplicitNullable = new Cast(CastKind.BoxingImplicitNullable);

    internal static readonly Cast BoxingExplicitNullable = new Cast(CastKind.BoxingExplicitNullable);

    internal static readonly Cast AnyBoxing = new Cast(CastKind.AnyBoxing);

    internal static readonly Cast AnyBoxingImplicitNullable = new Cast(CastKind.AnyBoxingImplicitNullable);

    internal static readonly Cast AnyBoxingExplicitNullable = new Cast(CastKind.AnyBoxingExplicitNullable);

    internal static readonly Cast Explicit = new Cast(CastKind.Explicit);

    internal static readonly Cast ExplicitNullable = new Cast(CastKind.ExplicitNullable);

    internal static readonly Cast ExplicitReference = new Cast(CastKind.ExplicitReference);

    internal static readonly Cast Unboxing = new Cast(CastKind.Unboxing);

    internal static readonly Cast UnboxingImplicitNullable = new Cast(CastKind.UnboxingImplicitNullable);

    internal static readonly Cast UnboxingExplicitNullable = new Cast(CastKind.UnboxingExplicitNullable);

    internal static readonly Cast AnyUnboxing = new Cast(CastKind.AnyUnboxing);

    internal static readonly Cast AnyUnboxingImplicitNullable = new Cast(CastKind.AnyUnboxingImplicitNullable);

    internal static readonly Cast AnyUnboxingExplicitNullable = new Cast(CastKind.AnyUnboxingExplicitNullable);

    private Cast(CastKind castKind) {
        kind = castKind;
    }

    internal CastKind kind { get; }

    /// <summary>
    /// If a cast exists (otherwise you cant go from one type to the other).
    /// </summary>
    internal bool exists => kind != CastKind.None;

    /// <summary>
    /// If the <see cref="Cast" /> is an identity cast.
    /// </summary>
    internal bool isIdentity => kind == CastKind.Identity;

    /// <summary>
    /// If the <see cref="Cast" /> is an implicit cast.
    /// </summary>
    internal bool isImplicit => kind.IsImplicitCast();

    /// <summary>
    /// If the <see cref="Cast" /> is an explicit cast.
    /// A <see cref="Cast" /> cannot be implicit and explicit.
    /// </summary>
    internal bool isExplicit => exists && !isImplicit;

    internal bool isNullable => kind.IsNullableCast();

    internal bool isReference => kind is CastKind.ImplicitReference or CastKind.ExplicitReference;

    internal bool isBoxing => kind.IsBoxingCast();

    internal bool isUnboxing => kind.IsUnboxingCast();

    /// <summary>
    /// Classify what type of <see cref="Cast" /> is required to go from one type to the other.
    /// </summary>
    internal static Cast Classify(TypeSymbol source, TypeSymbol target) {
        if (source.typeKind == TypeKind.Primitive && target.typeKind == TypeKind.Primitive)
            return new Cast(EasyOut.Classify(source, target));

        if (source.typeKind == TypeKind.Primitive || target.typeKind == TypeKind.Primitive)
            return None;

        if (source == target)
            return Identity;

        var sourceIsNullable = source.typeWithAnnotations.isNullable;
        var targetIsNullable = target.typeWithAnnotations.isNullable;

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
