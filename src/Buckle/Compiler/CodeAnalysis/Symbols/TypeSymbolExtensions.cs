
namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// Extensions on the <see cref="TypeSymbol" /> class.
/// </summary>
internal static class TypeSymbolExtensions {
    internal static int TypeToIndex(this TypeSymbol type) {
        switch (type.specialType) {
            case SpecialType.Any: return 0;
            case SpecialType.String: return 1;
            case SpecialType.Bool: return 2;
            case SpecialType.Char: return 3;
            case SpecialType.Int: return 4;
            case SpecialType.Decimal: return 5;
            case SpecialType.Type: return 6;
            case SpecialType.Nullable:
                var underlyingType = type.typeWithAnnotations.underlyingType;

                switch (underlyingType.specialType) {
                    case SpecialType.Any: return 7;
                    case SpecialType.String: return 8;
                    case SpecialType.Bool: return 9;
                    case SpecialType.Char: return 10;
                    case SpecialType.Int: return 11;
                    case SpecialType.Decimal: return 12;
                    case SpecialType.Type: return 13;
                }

                goto default;
            default: return -1;
        }
    }
}
