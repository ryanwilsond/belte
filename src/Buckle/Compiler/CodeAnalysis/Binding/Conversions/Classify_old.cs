
/// <summary>
/// Classify what type of <see cref="Cast" /> is required to go from one type to the other.
/// </summary>
internal static Cast Classify(TypeSymbol source, TypeSymbol target)
{
    var from = fromType.typeSymbol;
    var to = toType.typeSymbol;

    if (from is null)
    {
        if (fromType.isNullable && !toType.isNullable && includeNullability)
            return None;

        return Identity;
    }

    if (from != TypeSymbol.Void && to == TypeSymbol.Any)
    {
        if (toType.isNullable)
            return new Cast(true, true, true, true, true);

        return AnyAdding;
    }

    if (from == TypeSymbol.Any && to != TypeSymbol.Void)
        return Explicit;

    Cast InternalClassify()
    {
        if (from == to)
            return Identity;

        if (TypeUtilities.TypeInheritsFrom(from, to))
            return Implicit;

        if (TypeUtilities.TypeInheritsFrom(to, from))
            return Explicit;

        if (from == TypeSymbol.Bool || from == TypeSymbol.Int || from == TypeSymbol.Decimal)
        {
            if (to == TypeSymbol.String)
                return Explicit;
        }

        if (from == TypeSymbol.String)
        {
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

    if (cast != None && includeNullability)
    {
        // var! -> var : null adding
        // var -> var! : explicit
        if (!fromType.isNullable && toType.isNullable && cast != Explicit)
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
        (fromType.isConstantReference && toType.isReference && !toType.isConstantReference))
    {
        cast = None;
    }

    return cast;
}
