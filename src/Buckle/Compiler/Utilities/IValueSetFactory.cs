using Buckle.CodeAnalysis;
using Buckle.CodeAnalysis.Binding;

namespace Buckle.Utilities;

internal interface IValueSetFactory {
    IValueSet Related(BinaryOperatorKind relation, ConstantValue value);

    bool Related(BinaryOperatorKind relation, ConstantValue left, ConstantValue right);

    IValueSet allValues { get; }

    IValueSet noValues { get; }
}
