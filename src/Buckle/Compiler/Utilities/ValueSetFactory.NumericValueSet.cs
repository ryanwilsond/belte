using System;
using System.Collections.Immutable;
using System.Linq;
using Buckle.CodeAnalysis;
using Buckle.CodeAnalysis.Binding;
using Microsoft.CodeAnalysis.PooledObjects;
using static Buckle.CodeAnalysis.Binding.BinaryOperatorKind;

namespace Buckle.Utilities;

internal static partial class ValueSetFactory {
    private sealed class NumericValueSet<T> : IValueSet<T> {
        private readonly ImmutableArray<(T first, T last)> _intervals;
        private readonly INumericTC<T> _tc;

        public static NumericValueSet<T> AllValues(INumericTC<T> tc) {
            return new NumericValueSet<T>(tc.minValue, tc.maxValue, tc);
        }

        public static NumericValueSet<T> NoValues(INumericTC<T> tc) {
            return new NumericValueSet<T>([], tc);
        }

        internal NumericValueSet(T first, T last, INumericTC<T> tc) : this([(first, last)], tc) { }

        internal NumericValueSet(ImmutableArray<(T first, T last)> intervals, INumericTC<T> tc) {
            _intervals = intervals;
            _tc = tc;
        }

        public bool isEmpty => _intervals.Length == 0;

        ConstantValue IValueSet.sample {
            get {
                if (isEmpty)
                    throw new ArgumentException();

                var gz = new NumericValueSetFactory<T>(_tc).Related(GreaterThanOrEqual, _tc.zero);
                var t = (NumericValueSet<T>)Intersect(gz);

                if (!t.isEmpty)
                    return _tc.ToConstantValue(t._intervals[0].first);

                return _tc.ToConstantValue(_intervals[_intervals.Length - 1].last);
            }
        }

        public bool Any(BinaryOperatorKind relation, T value) {
            switch (relation) {
                case LessThan:
                case LessThanOrEqual:
                    return _intervals.Length > 0 && _tc.Related(relation, _intervals[0].first, value);
                case GreaterThan:
                case GreaterThanOrEqual:
                    return _intervals.Length > 0 && _tc.Related(relation, _intervals[_intervals.Length - 1].last, value);
                case Equal:
                    return AnyIntervalContains(0, _intervals.Length - 1, value);
                default:
                    throw ExceptionUtilities.UnexpectedValue(relation);
            }

            bool AnyIntervalContains(int firstIntervalIndex, int lastIntervalIndex, T value) {
                while (true) {
                    if (lastIntervalIndex < firstIntervalIndex)
                        return false;

                    if (lastIntervalIndex == firstIntervalIndex) {
                        return _tc.Related(GreaterThanOrEqual, value, _intervals[lastIntervalIndex].first) &&
                            _tc.Related(LessThanOrEqual, value, _intervals[lastIntervalIndex].last);
                    }

                    var midIndex = firstIntervalIndex + (lastIntervalIndex - firstIntervalIndex) / 2;

                    if (_tc.Related(LessThanOrEqual, value, _intervals[midIndex].last))
                        lastIntervalIndex = midIndex;
                    else
                        firstIntervalIndex = midIndex + 1;
                }
            }
        }

        bool IValueSet.Any(BinaryOperatorKind relation, ConstantValue value) {
            return value is null || Any(relation, _tc.FromConstantValue(value));
        }

        public bool All(BinaryOperatorKind relation, T value) {
            if (_intervals.Length == 0)
                return true;

            switch (relation) {
                case LessThan:
                case LessThanOrEqual:
                    return _tc.Related(relation, _intervals[_intervals.Length - 1].last, value);
                case GreaterThan:
                case GreaterThanOrEqual:
                    return _tc.Related(relation, _intervals[0].first, value);
                case Equal:
                    return _intervals.Length == 1 && _tc.Related(Equal, _intervals[0].first, value) &&
                        _tc.Related(Equal, _intervals[0].last, value);
                default:
                    throw ExceptionUtilities.UnexpectedValue(relation);
            }
        }

        bool IValueSet.All(BinaryOperatorKind relation, ConstantValue value) {
            return value is not null && All(relation, _tc.FromConstantValue(value));
        }

        public IValueSet<T> Complement() {
            if (_intervals.Length == 0)
                return AllValues(_tc);

            var builder = ArrayBuilder<(T first, T last)>.GetInstance();

            if (_tc.Related(LessThan, _tc.minValue, _intervals[0].first))
                builder.Add((_tc.minValue, _tc.Prev(_intervals[0].first)));

            var lastIndex = _intervals.Length - 1;

            for (var i = 0; i < lastIndex; i++)
                builder.Add((_tc.Next(_intervals[i].last), _tc.Prev(_intervals[i + 1].first)));

            if (_tc.Related(LessThan, _intervals[lastIndex].last, _tc.maxValue))
                builder.Add((_tc.Next(_intervals[lastIndex].last), _tc.maxValue));

            return new NumericValueSet<T>(builder.ToImmutableAndFree(), _tc);
        }

        IValueSet IValueSet.Complement() {
            return Complement();
        }

        public IValueSet<T> Intersect(IValueSet<T> o) {
            var other = (NumericValueSet<T>)o;

            var builder = ArrayBuilder<(T first, T last)>.GetInstance();
            var left = _intervals;
            var right = other._intervals;
            var l = 0;
            var r = 0;

            while (l < left.Length && r < right.Length) {
                var leftInterval = left[l];
                var rightInterval = right[r];

                if (_tc.Related(LessThan, leftInterval.last, rightInterval.first)) {
                    l++;
                } else if (_tc.Related(LessThan, rightInterval.last, leftInterval.first)) {
                    r++;
                } else {
                    Add(
                        builder, Max(leftInterval.first, rightInterval.first, _tc),
                        Min(leftInterval.last, rightInterval.last, _tc),
                        _tc
                    );

                    if (_tc.Related(LessThan, leftInterval.last, rightInterval.last)) {
                        l++;
                    } else if (_tc.Related(LessThan, rightInterval.last, leftInterval.last)) {
                        r++;
                    } else {
                        l++;
                        r++;
                    }
                }
            }

            return new NumericValueSet<T>(builder.ToImmutableAndFree(), _tc);
        }

        private static void Add(ArrayBuilder<(T first, T last)> builder, T first, T last, INumericTC<T> tc) {
            if (builder.Count > 0 && (tc.Related(Equal, tc.minValue, first) ||
                tc.Related(GreaterThanOrEqual, builder.Last().last, tc.Prev(first)))) {
                var oldLastInterval = builder.Pop();
                oldLastInterval.last = Max(last, oldLastInterval.last, tc);
                builder.Push(oldLastInterval);
            } else {
                builder.Add((first, last));
            }
        }

        private static T Min(T a, T b, INumericTC<T> tc) {
            return tc.Related(LessThan, a, b) ? a : b;
        }

        private static T Max(T a, T b, INumericTC<T> tc) {
            return tc.Related(LessThan, a, b) ? b : a;
        }

        IValueSet IValueSet.Intersect(IValueSet other) {
            return Intersect((IValueSet<T>)other);
        }

        public IValueSet<T> Union(IValueSet<T> o) {
            var other = (NumericValueSet<T>)o;
            var builder = ArrayBuilder<(T first, T last)>.GetInstance();
            var left = _intervals;
            var right = other._intervals;
            var l = 0;
            var r = 0;

            while (l < left.Length && r < right.Length) {
                var leftInterval = left[l];
                var rightInterval = right[r];

                if (_tc.Related(LessThan, leftInterval.last, rightInterval.first)) {
                    Add(builder, leftInterval.first, leftInterval.last, _tc);
                    l++;
                } else if (_tc.Related(LessThan, rightInterval.last, leftInterval.first)) {
                    Add(builder, rightInterval.first, rightInterval.last, _tc);
                    r++;
                } else {
                    Add(
                        builder,
                        Min(leftInterval.first, rightInterval.first, _tc),
                        Max(leftInterval.last, rightInterval.last, _tc),
                        _tc
                    );

                    l++;
                    r++;
                }
            }

            while (l < left.Length) {
                var leftInterval = left[l];
                Add(builder, leftInterval.first, leftInterval.last, _tc);
                l++;
            }

            while (r < right.Length) {
                var rightInterval = right[r];
                Add(builder, rightInterval.first, rightInterval.last, _tc);
                r++;
            }

            return new NumericValueSet<T>(builder.ToImmutableAndFree(), _tc);
        }

        IValueSet IValueSet.Union(IValueSet other) {
            return Union((IValueSet<T>)other);
        }

        public override string ToString() {
            return string.Join(",", _intervals.Select(p => $"[{_tc.ToString(p.first)}..{_tc.ToString(p.last)}]"));
        }

        public override bool Equals(object? obj) {
            return obj is NumericValueSet<T> other &&
            _intervals.SequenceEqual(other._intervals);
        }

        public override int GetHashCode() {
            return Hash.Combine(Hash.CombineValues(_intervals), _intervals.Length);
        }
    }
}
