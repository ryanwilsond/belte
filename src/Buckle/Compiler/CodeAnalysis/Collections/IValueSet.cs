using Buckle.CodeAnalysis.Binding;

namespace Buckle.CodeAnalysis;

internal interface IValueSet {
    IValueSet Intersect(IValueSet other);

    IValueSet Union(IValueSet other);

    IValueSet Complement();

    bool Any(BinaryOperatorKind relation, ConstantValue value);

    bool All(BinaryOperatorKind relation, ConstantValue value);

    bool isEmpty { get; }

    ConstantValue sample { get; }
}
