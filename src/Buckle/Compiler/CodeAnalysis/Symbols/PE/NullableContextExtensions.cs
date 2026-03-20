using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Symbols;

internal static class NullableContextExtensions {
    internal const byte NotAnnotatedAttributeValue = 1;
    internal const byte AnnotatedAttributeValue = 2;
    internal const byte ObliviousAttributeValue = 0;

    internal static NullableContextKind ToNullableContextFlags(this byte? value) {
        return value switch {
            null => NullableContextKind.None,
            ObliviousAttributeValue => NullableContextKind.Oblivious,
            NotAnnotatedAttributeValue => NullableContextKind.NotAnnotated,
            AnnotatedAttributeValue => NullableContextKind.Annotated,
            _ => throw ExceptionUtilities.UnexpectedValue(value),
        };
    }

    internal static bool TryGetByte(this NullableContextKind kind, out byte? value) {
        switch (kind) {
            case NullableContextKind.Unknown:
                value = null;
                return false;
            case NullableContextKind.None:
                value = null;
                return true;
            case NullableContextKind.Oblivious:
                value = ObliviousAttributeValue;
                return true;
            case NullableContextKind.NotAnnotated:
                value = NotAnnotatedAttributeValue;
                return true;
            case NullableContextKind.Annotated:
                value = AnnotatedAttributeValue;
                return true;
            default:
                throw ExceptionUtilities.UnexpectedValue(kind);
        }
    }
}
