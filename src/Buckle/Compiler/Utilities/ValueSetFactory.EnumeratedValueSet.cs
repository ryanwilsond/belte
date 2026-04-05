using System;
using System.Collections.Immutable;
using System.Linq;
using Buckle.CodeAnalysis;
using Buckle.CodeAnalysis.Binding;

namespace Buckle.Utilities;

internal static partial class ValueSetFactory {
    private sealed class EnumeratedValueSet<T> : IValueSet<T> where T : notnull {
        private readonly bool _included;

        private readonly ImmutableHashSet<T> _membersIncludedOrExcluded;

        private readonly IEquatableValueTC<T> _tc;

        private EnumeratedValueSet(bool included, ImmutableHashSet<T> membersIncludedOrExcluded, IEquatableValueTC<T> tc) {
            (_included, _membersIncludedOrExcluded, _tc) = (included, membersIncludedOrExcluded, tc);
        }

        public static EnumeratedValueSet<T> AllValues(IEquatableValueTC<T> tc) {
            return new EnumeratedValueSet<T>(included: false, ImmutableHashSet<T>.Empty, tc);
        }

        public static EnumeratedValueSet<T> NoValues(IEquatableValueTC<T> tc) {
            return new EnumeratedValueSet<T>(included: true, ImmutableHashSet<T>.Empty, tc);
        }

        internal static EnumeratedValueSet<T> Including(T value, IEquatableValueTC<T> tc) {
            return new EnumeratedValueSet<T>(included: true, ImmutableHashSet<T>.Empty.Add(value), tc);
        }

        public bool isEmpty => _included && _membersIncludedOrExcluded.IsEmpty;

        ConstantValue IValueSet.sample {
            get {
                if (isEmpty) throw new ArgumentException();

                if (_included)
                    return _tc.ToConstantValue(_membersIncludedOrExcluded.OrderBy(k => k).First());

                if (typeof(T) == typeof(string)) {
                    if (Any(BinaryOperatorKind.Equal, (T)(object)""))
                        return _tc.ToConstantValue((T)(object)"");

                    for (var c = 'A'; c <= 'z'; c++) {
                        if (Any(BinaryOperatorKind.Equal, (T)(object)c.ToString()))
                            return _tc.ToConstantValue((T)(object)c.ToString());
                    }
                }

                // var candidates = _tc.RandomValues(_membersIncludedOrExcluded.Count + 1, new Random(0), _membersIncludedOrExcluded.Count + 1);

                // foreach (var value in candidates) {
                //     if (this.Any(BinaryOperatorKind.Equal, value))
                //         return _tc.ToConstantValue(value);
                // }

                throw ExceptionUtilities.Unreachable();
            }
        }

        public bool Any(BinaryOperatorKind relation, T value) {
            switch (relation) {
                case BinaryOperatorKind.Equal:
                    return _included == _membersIncludedOrExcluded.Contains(value);
                default:
                    return true;
            }
        }

        bool IValueSet.Any(BinaryOperatorKind relation, ConstantValue value) {
            return value is null || Any(relation, _tc.FromConstantValue(value));
        }

        public bool All(BinaryOperatorKind relation, T value) {
            switch (relation) {
                case BinaryOperatorKind.Equal:
                    if (!_included)
                        return false;

                    switch (_membersIncludedOrExcluded.Count) {
                        case 0:
                            return true;
                        case 1:
                            return _membersIncludedOrExcluded.Contains(value);
                        default:
                            return false;
                    }
                default:
                    return false;
            }
        }

        bool IValueSet.All(BinaryOperatorKind relation, ConstantValue value) {
            return value is not null && All(relation, _tc.FromConstantValue(value));
        }

        public IValueSet<T> Complement() {
            return new EnumeratedValueSet<T>(!_included, _membersIncludedOrExcluded, _tc);
        }

        IValueSet IValueSet.Complement() {
            return Complement();
        }

        public IValueSet<T> Intersect(IValueSet<T> o) {
            if (this == o)
                return this;

            var other = (EnumeratedValueSet<T>)o;

            var (larger, smaller) = (_membersIncludedOrExcluded.Count > other._membersIncludedOrExcluded.Count)
                ? (this, other)
                : (other, this);

            switch (larger._included, smaller._included) {
                case (true, true):
                    return new EnumeratedValueSet<T>(true, larger._membersIncludedOrExcluded.Intersect(smaller._membersIncludedOrExcluded), _tc);
                case (true, false):
                    return new EnumeratedValueSet<T>(true, larger._membersIncludedOrExcluded.Except(smaller._membersIncludedOrExcluded), _tc);
                case (false, false):
                    return new EnumeratedValueSet<T>(false, larger._membersIncludedOrExcluded.Union(smaller._membersIncludedOrExcluded), _tc);
                case (false, true):
                    return new EnumeratedValueSet<T>(true, smaller._membersIncludedOrExcluded.Except(larger._membersIncludedOrExcluded), _tc);
            }
        }

        IValueSet IValueSet.Intersect(IValueSet other) {
            return Intersect((IValueSet<T>)other);
        }

        public IValueSet<T> Union(IValueSet<T> o) {
            if (this == o)
                return this;

            var other = (EnumeratedValueSet<T>)o;

            var (larger, smaller) = (_membersIncludedOrExcluded.Count > other._membersIncludedOrExcluded.Count)
                ? (this, other)
                : (other, this);

            switch (larger._included, smaller._included) {
                case (false, false):
                    return new EnumeratedValueSet<T>(false, larger._membersIncludedOrExcluded.Intersect(smaller._membersIncludedOrExcluded), _tc);
                case (false, true):
                    return new EnumeratedValueSet<T>(false, larger._membersIncludedOrExcluded.Except(smaller._membersIncludedOrExcluded), _tc);
                case (true, true):
                    return new EnumeratedValueSet<T>(true, larger._membersIncludedOrExcluded.Union(smaller._membersIncludedOrExcluded), _tc);
                case (true, false):
                    return new EnumeratedValueSet<T>(false, smaller._membersIncludedOrExcluded.Except(larger._membersIncludedOrExcluded), _tc);
            }
        }

        IValueSet IValueSet.Union(IValueSet other) {
            return Union((IValueSet<T>)other);
        }

        public override bool Equals(object? obj) {
            if (obj is not EnumeratedValueSet<T> other)
                return false;

            return _included == other._included &&
                _membersIncludedOrExcluded.SetEqualsWithoutIntermediateHashSet(other._membersIncludedOrExcluded);
        }

        public override int GetHashCode() {
            return Hash.Combine(_included.GetHashCode(), _membersIncludedOrExcluded.GetHashCode());
        }

        public override string ToString() {
            return $"{(_included ? "" : "~")}{{{string.Join(",", _membersIncludedOrExcluded.Select(o => o.ToString()))}{"}"}";
        }
    }
}
