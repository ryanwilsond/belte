using System.Collections.Generic;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed partial class TypeOrConstant {
    internal sealed class EqualsComparer : EqualityComparer<TypeOrConstant> {
        internal static readonly EqualsComparer ConsiderEverythingComparer
            = new EqualsComparer(TypeCompareKind.ConsiderEverything);

        private readonly TypeCompareKind _compareKind;

        private EqualsComparer(TypeCompareKind compareKind) {
            _compareKind = compareKind;
        }

        public override int GetHashCode(TypeOrConstant obj) {
            return obj.GetHashCode();
        }

        public override bool Equals(TypeOrConstant x, TypeOrConstant y) {
            return x.Equals(y, _compareKind);
        }
    }
}
