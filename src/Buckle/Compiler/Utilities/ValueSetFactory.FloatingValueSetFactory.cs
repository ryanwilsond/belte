using Buckle.CodeAnalysis;
using Buckle.CodeAnalysis.Binding;

namespace Buckle.Utilities;

internal static partial class ValueSetFactory {
    private sealed class FloatingValueSetFactory<TFloating> : IValueSetFactory<TFloating> {
        private readonly FloatingTC<TFloating> _tc;

        public FloatingValueSetFactory(FloatingTC<TFloating> tc) {
            _tc = tc;
        }

        IValueSet IValueSetFactory.allValues => FloatingValueSet<TFloating>.AllValues(_tc);

        IValueSet IValueSetFactory.noValues => FloatingValueSet<TFloating>.NoValues(_tc);

        public IValueSet<TFloating> Related(BinaryOperatorKind relation, TFloating value) {
            return FloatingValueSet<TFloating>.Related(relation, value, _tc);
        }

        IValueSet IValueSetFactory.Related(BinaryOperatorKind relation, ConstantValue value) {
            return value is null
                ? FloatingValueSet<TFloating>.AllValues(_tc)
                : FloatingValueSet<TFloating>.Related(relation, _tc.FromConstantValue(value), _tc);
        }

        bool IValueSetFactory.Related(BinaryOperatorKind relation, ConstantValue left, ConstantValue right) {
            return _tc.Related(relation, _tc.FromConstantValue(left), _tc.FromConstantValue(right));
        }
    }
}
