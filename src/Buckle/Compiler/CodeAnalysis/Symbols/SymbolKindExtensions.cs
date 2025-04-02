using Buckle.Diagnostics;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Symbols;

internal static class SymbolKindExtensions {
    internal static int ToSortOrder(this SymbolKind kind) {
        return kind switch {
            SymbolKind.Field => 0,
            SymbolKind.Method => 1,
            SymbolKind.NamedType => 2,
            SymbolKind.ArrayType => 3,
            SymbolKind.ErrorType => 4,
            SymbolKind.Label => 5,
            SymbolKind.Local => 6,
            SymbolKind.Parameter => 7,
            SymbolKind.TemplateParameter => 8,
            _ => throw ExceptionUtilities.UnexpectedValue(kind),
        };
    }

    internal static string Localize(this SymbolKind kind) {
        return kind switch {
            SymbolKind.Field => MessageID.IDS_SK_FIELD.Localize(),
            SymbolKind.Method => MessageID.IDS_SK_METHOD.Localize(),
            SymbolKind.NamedType => MessageID.IDS_SK_TYPE.Localize(),
            SymbolKind.ArrayType => MessageID.IDS_SK_ARRAY.Localize(),
            SymbolKind.Label => MessageID.IDS_SK_LABEL.Localize(),
            SymbolKind.Local or SymbolKind.Parameter => MessageID.IDS_SK_VARIABLE.Localize(),
            SymbolKind.TemplateParameter => MessageID.IDS_SK_TEMPVAR.Localize(),
            _ => MessageID.IDS_SK_UNKNOWN.Localize(),
        };
    }
}
