using System;
using System.Text;
using Buckle.CodeAnalysis;
using Buckle.CodeAnalysis.Binding;
using static Buckle.CodeAnalysis.Binding.BinaryOperatorKind;

namespace Buckle.Utilities;

internal static partial class ValueSetFactory {
    private sealed class FloatingValueSet<TFloating> : IValueSet<TFloating> {
        private readonly IValueSet<TFloating> _numbers;
        private readonly bool _hasNaN;
        private readonly FloatingTC<TFloating> _tc;

        private FloatingValueSet(IValueSet<TFloating> numbers, bool hasNaN, FloatingTC<TFloating> tc) {
            (_numbers, _hasNaN, _tc) = (numbers, hasNaN, tc);
        }

        internal static IValueSet<TFloating> AllValues(FloatingTC<TFloating> tc) {
            return new FloatingValueSet<TFloating>(
            numbers: NumericValueSet<TFloating>.AllValues(tc), hasNaN: true, tc);
        }

        internal static IValueSet<TFloating> NoValues(FloatingTC<TFloating> tc) {
            return new FloatingValueSet<TFloating>(
            numbers: NumericValueSet<TFloating>.NoValues(tc), hasNaN: false, tc);
        }

        public bool isEmpty => !_hasNaN && _numbers.isEmpty;

        ConstantValue IValueSet.sample {
            get {
                if (isEmpty)
                    throw new ArgumentException();

                if (!_numbers.isEmpty) {
                    var sample = _numbers.sample;
                    return sample;
                }

                return _tc.ToConstantValue(_tc.NaN);
            }
        }

        public static IValueSet<TFloating> Related(BinaryOperatorKind relation, TFloating value, FloatingTC<TFloating> tc) {
            if (tc.Related(Equal, tc.NaN, value)) {
                switch (relation) {
                    case Equal:
                    case LessThanOrEqual:
                    case GreaterThanOrEqual:
                        return new FloatingValueSet<TFloating>(
                            hasNaN: true,
                            numbers: NumericValueSet<TFloating>.NoValues(tc),
                            tc: tc
                            );
                    case LessThan:
                    case GreaterThan:
                        return NoValues(tc);
                    default:
                        throw ExceptionUtilities.UnexpectedValue(relation);
                }
            }

            return new FloatingValueSet<TFloating>(
                numbers: new NumericValueSetFactory<TFloating>(tc).Related(relation, value),
                hasNaN: false,
                tc: tc
            );
        }

        public IValueSet<TFloating> Intersect(IValueSet<TFloating> o) {
            if (this == o)
                return this;

            var other = (FloatingValueSet<TFloating>)o;

            return new FloatingValueSet<TFloating>(
                numbers: _numbers.Intersect(other._numbers),
                hasNaN: _hasNaN & other._hasNaN,
                _tc
            );
        }

        IValueSet IValueSet.Intersect(IValueSet other) {
            return Intersect((IValueSet<TFloating>)other);
        }

        public IValueSet<TFloating> Union(IValueSet<TFloating> o) {
            if (this == o)
                return this;

            var other = (FloatingValueSet<TFloating>)o;

            return new FloatingValueSet<TFloating>(
                numbers: _numbers.Union(other._numbers),
                hasNaN: _hasNaN | other._hasNaN,
                _tc
            );
        }

        IValueSet IValueSet.Union(IValueSet other) {
            return Union((IValueSet<TFloating>)other);
        }

        public IValueSet<TFloating> Complement() {
            return new FloatingValueSet<TFloating>(
                numbers: _numbers.Complement(),
                hasNaN: !_hasNaN,
                _tc);
        }

        IValueSet IValueSet.Complement() {
            return Complement();
        }

        bool IValueSet.Any(BinaryOperatorKind relation, ConstantValue value) {
            return value is null || Any(relation, _tc.FromConstantValue(value));
        }

        public bool Any(BinaryOperatorKind relation, TFloating value) {
            return
                _hasNaN && _tc.Related(relation, _tc.NaN, value) ||
                _numbers.Any(relation, value);
        }

        bool IValueSet.All(BinaryOperatorKind relation, ConstantValue value) {
            return value is not null && All(relation, _tc.FromConstantValue(value));
        }

        public bool All(BinaryOperatorKind relation, TFloating value) {
            return
                (!_hasNaN || _tc.Related(relation, _tc.NaN, value)) &&
                _numbers.All(relation, value);
        }

        public override int GetHashCode() {
            return _numbers.GetHashCode();
        }

        public override bool Equals(object? obj) {
            return this == obj ||
                obj is FloatingValueSet<TFloating> other &&
                _hasNaN == other._hasNaN &&
                _numbers.Equals(other._numbers);
        }

        public override string ToString() {
            var b = new StringBuilder();

            if (_hasNaN)
                b.Append("NaN");

            var more = _numbers.ToString()!;

            if (b.Length > 1 && more.Length > 1)
                b.Append(',');

            b.Append(more);
            return b.ToString();
        }
    }
}
