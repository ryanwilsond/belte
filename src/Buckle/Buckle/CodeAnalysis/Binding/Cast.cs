using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A cast from any <see cref="BoundTypeClause" /> to any <see cref="BoundTypeClause" /> (can be the same).
/// </summary>
internal sealed class Cast {
    /// <summary>
    /// No cast.
    /// </summary>
    internal static readonly Cast None = new Cast(false, false, false);

    /// <summary>
    /// <see cref="Cast" /> where both types are the same.
    /// </summary>
    internal static readonly Cast Identity = new Cast(true, true, true);

    /// <summary>
    /// Lossless cast, can be done automatically.
    /// </summary>
    internal static readonly Cast Implicit = new Cast(true, false, true);

    /// <summary>
    /// Lossy cast, cannot be done implicitly.
    /// </summary>
    internal static readonly Cast Explicit = new Cast(true, false, false);

    private Cast(bool exists, bool isIdentity, bool isImplicit) {
        this.exists = exists;
        this.isIdentity = isIdentity;
        this.isImplicit = isImplicit;
    }

    /// <summary>
    /// If a cast exists (otherwise you cant go from one type to the other).
    /// </summary>
    internal bool exists { get; }

    /// <summary>
    /// If the <see cref="Cast" /> is an identity cast.
    /// </summary>
    internal bool isIdentity { get; }

    /// <summary>
    /// If the <see cref="Cast" /> is an implicit cast.
    /// </summary>
    internal bool isImplicit { get; }

    /// <summary>
    /// If the <see cref="Cast" /> is an explicit cast.
    /// A <see cref="Cast" /> cannot be implicit and explicit.
    /// </summary>
    internal bool isExplicit => exists && !isImplicit;

    /// <summary>
    /// Classify what type of <see cref="Cast" /> is required to go from one type to the other.
    /// </summary>
    /// <param name="fromType">Target <see cref="BoundTypeClause" />.</param>
    /// <param name="toType">Existing/current <see cref="BoundTypeClause" />.</param>
    /// <returns>Created <see cref="Cast" />.</returns>
    internal static Cast Classify(BoundTypeClause fromType, BoundTypeClause toType) {
        var from = fromType.type;
        var to = toType.type;

        if (from == null)
            return Cast.Identity;

        Cast InternalClassify() {
            if (from == to)
                return Cast.Identity;
            if (from != TypeSymbol.Void && to == TypeSymbol.Any)
                return Cast.Implicit;
            if (from == TypeSymbol.Any && to != TypeSymbol.Void)
                return Cast.Explicit;
            if (from == TypeSymbol.Bool || from == TypeSymbol.Int || from == TypeSymbol.Decimal)
                if (to == TypeSymbol.String)
                    return Cast.Explicit;
            if (from == TypeSymbol.String)
                if (to == TypeSymbol.Bool || to == TypeSymbol.Int || to == TypeSymbol.Decimal)
                    return Cast.Explicit;
            if (from == TypeSymbol.Int && to == TypeSymbol.Decimal)
                return Cast.Implicit;
            if (from == TypeSymbol.Decimal && to == TypeSymbol.Int)
                return Cast.Explicit;

            return Cast.None;
        }

        var cast = InternalClassify();

        if (cast != Cast.None) {
            // [NotNull]var -> var : implicit
            // var -> [NotNull]var : explicit
            if (!fromType.isLiteral && !fromType.isNullable && toType.isNullable && cast != Cast.Explicit)
                cast = Cast.Implicit;

            if (fromType.isNullable && !toType.isNullable && !toType.isLiteral)
                cast = Cast.Explicit;
        }

        return cast;
    }
}
