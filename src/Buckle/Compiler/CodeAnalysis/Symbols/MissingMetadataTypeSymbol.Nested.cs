using System;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract partial class MissingMetadataTypeSymbol {
    internal sealed class Nested : MissingMetadataTypeSymbol {
        private readonly NamedTypeSymbol _containingType;

        internal Nested(NamedTypeSymbol containingType, string name, int arity, bool mangleName)
            : base(name, arity, mangleName) {
            _containingType = containingType;
        }

        internal Nested(NamedTypeSymbol containingType, ref MetadataTypeName emittedName)
            : this(
                containingType,
                ref emittedName,
                emittedName.forcedArity == -1 || emittedName.forcedArity == emittedName.inferredArity
            ) {
        }

        private Nested(NamedTypeSymbol containingType, ref MetadataTypeName emittedName, bool mangleName)
            : this(containingType,
                   mangleName ? emittedName.unmangledTypeName : emittedName.typeName,
                   mangleName ? emittedName.inferredArity : emittedName.forcedArity,
                   mangleName) {
        }

        internal override Symbol containingSymbol => _containingType;

        public override int GetHashCode() {
            return Hash.Combine(_containingType, Hash.Combine(metadataName, _arity));
        }

        internal override bool Equals(TypeSymbol t2, TypeCompareKind comparison) {
            if (ReferenceEquals(this, t2))
                return true;

            return t2 is Nested other && string.Equals(metadataName, other.metadataName, StringComparison.Ordinal) &&
                _arity == other._arity &&
                _containingType.Equals(other._containingType, comparison);
        }
    }
}
