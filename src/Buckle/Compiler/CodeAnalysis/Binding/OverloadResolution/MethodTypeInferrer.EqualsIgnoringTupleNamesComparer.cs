using System.Collections.Generic;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

internal partial struct MethodTypeInferrer {
    private sealed class EqualsIgnoringTupleNamesComparer : EqualityComparer<TypeOrConstant> {
        internal static readonly EqualsIgnoringTupleNamesComparer Instance = new EqualsIgnoringTupleNamesComparer();

        public override int GetHashCode(TypeOrConstant obj) {
            return obj.GetHashCode();
        }

        public override bool Equals(TypeOrConstant x, TypeOrConstant y) {
            return x.Equals(y, TypeCompareKind.IgnoreTupleNames);
        }
    }
}
