using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A cast from any <see cref="BoundType" /> to any <see cref="BoundType" /> (can be the same).
/// </summary>
internal sealed class Cast {
    /// <summary>
    /// No cast.
    /// </summary>
    internal static readonly Cast None = new Cast(false, false, false, false);

    /// <summary>
    /// <see cref="Cast" /> where both types are the same.
    /// </summary>
    internal static readonly Cast Identity = new Cast(true, true, true, false);

    /// <summary>
    /// Lossless cast, can be done automatically.
    /// </summary>
    internal static readonly Cast Implicit = new Cast(true, false, true, false);

    /// <summary>
    /// Lossless cast where nullability is being added.
    /// </summary>
    internal static readonly Cast NullAdding = new Cast(true, true, true, true);

    /// <summary>
    /// Lossy cast, cannot be done implicitly.
    /// </summary>
    internal static readonly Cast Explicit = new Cast(true, false, false, false);

    private Cast(bool exists, bool isIdentity, bool isImplicit, bool isNullAdding) {
        this.exists = exists;
        this.isIdentity = isIdentity;
        this.isImplicit = isImplicit;
        this.isNullAdding = isNullAdding;
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
    /// If the <see cref="Cast"/> is a nullable cast.
    /// </summary>
    internal bool isNullAdding { get; }

    /// <summary>
    /// If the <see cref="Cast" /> is an explicit cast.
    /// A <see cref="Cast" /> cannot be implicit and explicit.
    /// </summary>
    internal bool isExplicit => exists && !isImplicit;

    /// <summary>
    /// Classify what type of <see cref="Cast" /> is required to go from one type to the other.
    /// </summary>
    /// <param name="fromType">Target <see cref="BoundType" />.</param>
    /// <param name="toType">Existing/current <see cref="BoundType" />.</param>
    /// <param name="includeNullability">
    /// If to account for nullability, otherwise both types are treated as non-nullable.
    /// </param>
    /// <returns>Created <see cref="Cast" />.</returns>
    internal static Cast Classify(BoundType fromType, BoundType toType, bool includeNullability = true) {
        var from = fromType.typeSymbol;
        var to = toType.typeSymbol;

        if (from is null) {
            if (fromType.isNullable && !toType.isNullable && includeNullability)
                return None;

            return Identity;
        }

        if (from != TypeSymbol.Void && to == TypeSymbol.Any)
            return Implicit;
        if (from == TypeSymbol.Any && to != TypeSymbol.Void)
            return Explicit;

        Cast InternalClassify() {
            if (from == to)
                return Identity;
            if (from == TypeSymbol.Bool || from == TypeSymbol.Int || from == TypeSymbol.Decimal) {
                if (to == TypeSymbol.String)
                    return Explicit;
            }

            if (from == TypeSymbol.String) {
                if (to == TypeSymbol.Bool || to == TypeSymbol.Int || to == TypeSymbol.Decimal)
                    return Explicit;
            }

            if (from == TypeSymbol.Int && to == TypeSymbol.Decimal)
                return Implicit;
            if (from == TypeSymbol.Decimal && to == TypeSymbol.Int)
                return Explicit;

            return None;
        }

        var cast = InternalClassify();

        if (cast != None && includeNullability) {
            // var! -> var : identity
            // var -> var! : explicit
            if (!fromType.isLiteral && !fromType.isNullable && toType.isNullable && cast != Explicit)
                cast = NullAdding;

            if (fromType.isNullable && !toType.isNullable && !toType.isLiteral)
                cast = Explicit;
        }

        // Special cases that are not allowed
        //      var -> ref var
        //      ref var -> var
        //      var[] -> var        (any dimension mismatch)
        //      ref const -> ref
        if ((toType.isReference && toType.isExplicitReference && !fromType.isReference) ||
            (fromType.isReference && fromType.isExplicitReference && !toType.isReference) ||
            fromType.dimensions != toType.dimensions ||
            (fromType.isConstantReference && toType.isReference && !toType.isConstantReference)) {
            cast = None;
        }

        return cast;
    }
}
