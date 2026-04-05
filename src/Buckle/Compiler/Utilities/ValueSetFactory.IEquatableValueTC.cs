using Buckle.CodeAnalysis;

namespace Buckle.Utilities;

internal static partial class ValueSetFactory {
    private interface IEquatableValueTC<T> where T : notnull {
        T FromConstantValue(ConstantValue constantValue);

        ConstantValue ToConstantValue(T value);
    }
}
