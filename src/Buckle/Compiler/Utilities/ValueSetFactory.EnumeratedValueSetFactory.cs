using Buckle.CodeAnalysis;
using Buckle.CodeAnalysis.Binding;
using static Buckle.CodeAnalysis.Binding.BinaryOperatorKind;

namespace Buckle.Utilities;

internal static partial class ValueSetFactory {
    private sealed class EnumeratedValueSetFactory<T> : IValueSetFactory<T> where T : notnull {
        private readonly IEquatableValueTC<T> _tc;

        IValueSet IValueSetFactory.allValues => EnumeratedValueSet<T>.AllValues(_tc);

        IValueSet IValueSetFactory.noValues => EnumeratedValueSet<T>.NoValues(_tc);

        public EnumeratedValueSetFactory(IEquatableValueTC<T> tc) { _tc = tc; }

        public IValueSet<T> Related(BinaryOperatorKind relation, T value) {
            switch (relation) {
                case Equal:
                    return EnumeratedValueSet<T>.Including(value, _tc);
                default:
                    return EnumeratedValueSet<T>.AllValues(_tc);
            }
        }

        IValueSet IValueSetFactory.Related(BinaryOperatorKind relation, ConstantValue value) {
            return (value is null) || ConstantValue.IsNull(value)
                ? EnumeratedValueSet<T>.AllValues(_tc)
                : Related(relation, _tc.FromConstantValue(value));
        }

        bool IValueSetFactory.Related(BinaryOperatorKind relation, ConstantValue left, ConstantValue right) {
            return _tc.FromConstantValue(left).Equals(_tc.FromConstantValue(right));
        }
    }
}
