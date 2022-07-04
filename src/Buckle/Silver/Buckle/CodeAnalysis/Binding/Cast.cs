using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

internal sealed class Cast {
    internal bool exists { get; }
    internal bool isIdentity { get; }
    internal bool isImplicit { get; }
    internal bool isExplicit => exists && !isImplicit;

    internal static readonly Cast None = new Cast(false, false, false);
    internal static readonly Cast Identity = new Cast(true, true, true);
    internal static readonly Cast Implicit = new Cast(true, false, true);
    internal static readonly Cast Explicit = new Cast(true, false, false);

    private Cast(bool exists_, bool isIdentity_, bool isImplicit_) {
        exists = exists_;
        isIdentity = isIdentity_;
        isImplicit = isImplicit_;
    }

    internal static Cast Classify(BoundTypeClause fromType, BoundTypeClause toType) {
        var from = fromType.lType;
        var to = toType.lType;

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
            if (!fromType.isLiteral && !fromType.isNullable && toType.isNullable)
                cast = Cast.Implicit;

            if (fromType.isNullable && !toType.isNullable && !toType.isLiteral)
                cast = Cast.Explicit;
        }

        return cast;
    }
}
