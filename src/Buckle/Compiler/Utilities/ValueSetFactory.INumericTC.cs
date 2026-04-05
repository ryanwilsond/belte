using Buckle.CodeAnalysis;
using Buckle.CodeAnalysis.Binding;

namespace Buckle.Utilities;

internal static partial class ValueSetFactory {
    private interface INumericTC<T> {
        T FromConstantValue(ConstantValue constantValue);

        ConstantValue ToConstantValue(T value);

        bool Related(BinaryOperatorKind relation, T left, T right);

        T minValue { get; }

        T maxValue { get; }

        T Next(T value);

        T Prev(T value);

        T zero { get; }

        string ToString(T value);
    }
}
