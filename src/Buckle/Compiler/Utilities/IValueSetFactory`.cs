using Buckle.CodeAnalysis.Binding;

namespace Buckle.Utilities;

internal interface IValueSetFactory<T> : IValueSetFactory {
    IValueSet<T> Related(BinaryOperatorKind relation, T value);
}
