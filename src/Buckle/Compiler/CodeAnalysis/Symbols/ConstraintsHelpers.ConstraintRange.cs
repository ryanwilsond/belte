using System;
using Buckle.CodeAnalysis.Binding;

namespace Buckle.CodeAnalysis.Symbols;

internal static partial class ConstraintsHelpers {
    internal struct ConstraintRange {
        internal BinaryOperatorKind kind;
        internal IComparable right;

        internal ConstraintRange(BinaryOperatorKind kind, IComparable right) {
            this.kind = kind;
            this.right = right;
        }
    }
}
