using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

internal sealed class Cast {
    public bool exists { get; }
    public bool isIdentity { get; }
    public bool isImplicit { get; }
    public bool isExplicit => exists && !isImplicit;

    public static readonly Cast None = new Cast(false, false, false);
    public static readonly Cast Identity = new Cast(true, true, true);
    public static readonly Cast Implicit = new Cast(true, false, true);
    public static readonly Cast Explicit = new Cast(true, false, false);

    private Cast(bool exists_, bool isIdentity_, bool isImplicit_) {
        exists = exists_;
        isIdentity = isIdentity_;
        isImplicit = isImplicit_;
    }

    public static Cast Classify(BoundTypeClause fromType, BoundTypeClause toType) {
        var from = fromType.lType;
        var to = toType.lType;
        var cast = Cast.None;

        if (from == to)
            cast = Cast.Identity;

        if (from == null)
            return Cast.Identity;

        if (from != TypeSymbol.Void && to == TypeSymbol.Any)
            cast = Cast.Implicit;
        else if (from == TypeSymbol.Any && to != TypeSymbol.Void)
            cast = Cast.Explicit;
        else if (from == TypeSymbol.Bool || from == TypeSymbol.Int || from == TypeSymbol.Decimal)
            if (to == TypeSymbol.String)
                cast = Cast.Explicit;
        else if (from == TypeSymbol.String)
            if (to == TypeSymbol.Bool || to == TypeSymbol.Int || to == TypeSymbol.Decimal)
                cast = Cast.Explicit;
        else if (from == TypeSymbol.Int && to == TypeSymbol.Decimal)
            cast = Cast.Implicit;
        else if (from == TypeSymbol.Decimal && to == TypeSymbol.Int)
            cast = Cast.Explicit;

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
