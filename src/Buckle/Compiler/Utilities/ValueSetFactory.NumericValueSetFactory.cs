using Buckle.CodeAnalysis;
using Buckle.CodeAnalysis.Binding;
using static Buckle.CodeAnalysis.Binding.BinaryOperatorKind;

namespace Buckle.Utilities;

internal static partial class ValueSetFactory {
    private struct NumericValueSetFactory<T> : IValueSetFactory<T> {
        private readonly INumericTC<T> _tc;

        IValueSet IValueSetFactory.allValues => NumericValueSet<T>.AllValues(_tc);

        IValueSet IValueSetFactory.noValues => NumericValueSet<T>.NoValues(_tc);

        internal NumericValueSetFactory(INumericTC<T> tc) { _tc = tc; }

        public IValueSet<T> Related(BinaryOperatorKind relation, T value) {
            switch (relation) {
                case LessThan:
                    if (_tc.Related(LessThanOrEqual, value, _tc.minValue))
                        return NumericValueSet<T>.NoValues(_tc);
                    return new NumericValueSet<T>(_tc.minValue, _tc.Prev(value), _tc);
                case LessThanOrEqual:
                    return new NumericValueSet<T>(_tc.minValue, value, _tc);
                case GreaterThan:
                    if (_tc.Related(GreaterThanOrEqual, value, _tc.maxValue))
                        return NumericValueSet<T>.NoValues(_tc);
                    return new NumericValueSet<T>(_tc.Next(value), _tc.maxValue, _tc);
                case GreaterThanOrEqual:
                    return new NumericValueSet<T>(value, _tc.maxValue, _tc);
                case Equal:
                    return new NumericValueSet<T>(value, value, _tc);
                default:
                    throw ExceptionUtilities.UnexpectedValue(relation);
            }
        }

        IValueSet IValueSetFactory.Related(BinaryOperatorKind relation, ConstantValue value) {
            return value is null ? NumericValueSet<T>.AllValues(_tc) : Related(relation, _tc.FromConstantValue(value));
        }

        bool IValueSetFactory.Related(BinaryOperatorKind relation, ConstantValue left, ConstantValue right) {
            return _tc.Related(relation, _tc.FromConstantValue(left), _tc.FromConstantValue(right));
        }
    }
}
