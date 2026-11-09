using System.Collections.Generic;

namespace Buckle.CodeAnalysis.Symbols;

#pragma warning disable CS0660

internal abstract partial class TypeSymbol {
    private protected class ExplicitInterfaceImplementationTargetMemberEqualityComparer : IEqualityComparer<Symbol> {
        internal static readonly ExplicitInterfaceImplementationTargetMemberEqualityComparer Instance
            = new ExplicitInterfaceImplementationTargetMemberEqualityComparer();

        private ExplicitInterfaceImplementationTargetMemberEqualityComparer() { }

        public bool Equals(Symbol x, Symbol y) {
            return x.originalDefinition == y.originalDefinition &&
                   x.containingType.Equals(y.containingType, TypeCompareKind.CLRSignatureCompareOptions);
        }

        public int GetHashCode(Symbol obj) {
            return obj.originalDefinition.GetHashCode();
        }
    }
}
