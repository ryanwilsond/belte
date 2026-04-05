using System;
using Buckle.CodeAnalysis;
using Buckle.CodeAnalysis.Binding;
using static Buckle.CodeAnalysis.Binding.BinaryOperatorKind;

namespace Buckle.Utilities;

internal static partial class ValueSetFactory {
    private sealed class BoolValueSet : IValueSet<bool> {
        private readonly bool _hasFalse, _hasTrue;

        internal static readonly BoolValueSet AllValues = new BoolValueSet(hasFalse: true, hasTrue: true);
        internal static readonly BoolValueSet None = new BoolValueSet(hasFalse: false, hasTrue: false);
        internal static readonly BoolValueSet OnlyTrue = new BoolValueSet(hasFalse: false, hasTrue: true);
        internal static readonly BoolValueSet OnlyFalse = new BoolValueSet(hasFalse: true, hasTrue: false);

        private BoolValueSet(bool hasFalse, bool hasTrue) => (_hasFalse, _hasTrue) = (hasFalse, hasTrue);

        public static BoolValueSet Create(bool hasFalse, bool hasTrue) {
            switch (hasFalse, hasTrue) {
                case (false, false):
                    return None;
                case (false, true):
                    return OnlyTrue;
                case (true, false):
                    return OnlyFalse;
                case (true, true):
                    return AllValues;
            }
        }

        bool IValueSet.isEmpty => !_hasFalse && !_hasTrue;

        ConstantValue IValueSet.sample => new ConstantValue(
            _hasTrue ? true : _hasFalse ? false : throw new ArgumentException(),
            CodeAnalysis.Symbols.SpecialType.Bool);

        public bool Any(BinaryOperatorKind relation, bool value) {
            switch (relation, value) {
                case (Equal, true):
                    return _hasTrue;
                case (Equal, false):
                    return _hasFalse;
                default:
                    return true;
            }
        }

        bool IValueSet.Any(BinaryOperatorKind relation, ConstantValue value) {
            return value is null || Any(relation, (bool)value.value);
        }

        public bool All(BinaryOperatorKind relation, bool value) {
            switch (relation, value) {
                case (Equal, true):
                    return !_hasFalse;
                case (Equal, false):
                    return !_hasTrue;
                default:
                    return true;
            }
        }

        bool IValueSet.All(BinaryOperatorKind relation, ConstantValue value) {
            return value is not null && All(relation, (bool)value.value);
        }

        public IValueSet<bool> Complement() {
            return Create(!_hasFalse, !_hasTrue);
        }

        IValueSet IValueSet.Complement() {
            return Complement();
        }

        public IValueSet<bool> Intersect(IValueSet<bool> other) {
            if (this == other)
                return this;

            var o = (BoolValueSet)other;
            return Create(hasFalse: _hasFalse & o._hasFalse, hasTrue: _hasTrue & o._hasTrue);
        }

        public IValueSet Intersect(IValueSet other) {
            return Intersect((IValueSet<bool>)other);
        }

        public IValueSet<bool> Union(IValueSet<bool> other) {
            if (this == other)
                return this;

            var o = (BoolValueSet)other;
            return Create(hasFalse: _hasFalse | o._hasFalse, hasTrue: _hasTrue | o._hasTrue);
        }

        IValueSet IValueSet.Union(IValueSet other) {
            return Union((IValueSet<bool>)other);
        }

        public override bool Equals(object? obj) {
            return this == obj;
        }

        public override int GetHashCode() {
            return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);
        }

        public override string ToString() {
            return (_hasFalse, _hasTrue) switch {
                (false, false) => "{}",
                (true, false) => "{false}",
                (false, true) => "{true}",
                (true, true) => "{false,true}",
            };
        }
    }
}
