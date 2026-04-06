using Buckle.CodeAnalysis;
using Buckle.CodeAnalysis.Binding;
using static Buckle.CodeAnalysis.Binding.BinaryOperatorKind;

namespace Buckle.Utilities;

internal static partial class ValueSetFactory {
    private sealed class BoolValueSetFactory : IValueSetFactory<bool> {
        public static readonly BoolValueSetFactory Instance = new BoolValueSetFactory();

        private BoolValueSetFactory() { }

        IValueSet IValueSetFactory.allValues => BoolValueSet.AllValues;

        IValueSet IValueSetFactory.noValues => BoolValueSet.None;

        public IValueSet<bool> Related(BinaryOperatorKind relation, bool value) {
            switch (relation, value) {
                case (Equal, true):
                    return BoolValueSet.OnlyTrue;
                case (Equal, false):
                    return BoolValueSet.OnlyFalse;
                default:
                    return BoolValueSet.AllValues;
            }
        }

        IValueSet IValueSetFactory.Related(BinaryOperatorKind relation, ConstantValue value) {
            return value is null ? BoolValueSet.AllValues : Related(relation, (bool)value.value);
        }

        bool IValueSetFactory.Related(BinaryOperatorKind relation, ConstantValue left, ConstantValue right) {
            return left is null || right is null || (bool)left.value == (bool)right.value;
        }
    }
}
