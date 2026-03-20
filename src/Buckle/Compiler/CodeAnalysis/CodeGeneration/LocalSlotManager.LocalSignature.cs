using System;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.CodeGeneration;

internal abstract partial class LocalSlotManager {
    private protected readonly struct LocalSignature : IEquatable<LocalSignature> {
        private readonly TypeSymbol _type;
        private readonly LocalSlotConstraints _constraints;

        internal LocalSignature(TypeSymbol valType, LocalSlotConstraints constraints) {
            _constraints = constraints;
            _type = valType;
        }

        public bool Equals(LocalSignature other) {
            return _constraints == other._constraints && _type.Equals(other._type);
        }

        public override int GetHashCode() {
            return Hash.Combine(_type.GetHashCode(), (int)_constraints);
        }

        public override bool Equals(object? obj) {
            return obj is LocalSignature ls && Equals(ls);
        }
    }
}
