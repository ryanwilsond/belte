using Buckle.CodeAnalysis.Symbols;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Binding;

internal partial class BoundDagTest {
    public override bool Equals(object obj) {
        return Equals(obj as BoundDagTest);
    }

    private bool Equals(BoundDagTest other) {
        if (other is null || kind != other.kind)
            return false;

        if (this == other)
            return true;

        if (!input.Equals(other.input))
            return false;

        switch (this, other) {
            case (BoundDagTypeTest x, BoundDagTypeTest y):
                return x.type.Equals(y.type, TypeCompareKind.AllIgnoreOptions);
            case (BoundDagNonNullTest x, BoundDagNonNullTest y):
                return x.isExplicitTest == y.isExplicitTest;
            case (BoundDagExplicitNullTest x, BoundDagExplicitNullTest y):
                return true;
            case (BoundDagValueTest x, BoundDagValueTest y):
                return x.value.Equals(y.value);
            default:
                throw ExceptionUtilities.UnexpectedValue(this);
        }
    }

    public override int GetHashCode() {
        return Hash.Combine(((int)kind).GetHashCode(), input.GetHashCode());
    }
}
