using Buckle.CodeAnalysis;
using Buckle.CodeAnalysis.Binding;

namespace Buckle.Utilities;

internal interface IValueSet<T> : IValueSet {
    IValueSet<T> Intersect(IValueSet<T> other);

    IValueSet<T> Union(IValueSet<T> other);

    new IValueSet<T> Complement();

    bool Any(BinaryOperatorKind relation, T value);

    bool All(BinaryOperatorKind relation, T value);
}
