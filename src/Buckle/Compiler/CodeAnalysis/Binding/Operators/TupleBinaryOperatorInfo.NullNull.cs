
namespace Buckle.CodeAnalysis.Binding;

internal abstract partial class TupleBinaryOperatorInfo {
    internal class NullNull : TupleBinaryOperatorInfo {
        internal readonly BinaryOperatorKind kind;

        internal NullNull(BinaryOperatorKind kind)
            : base(leftConvertedType: null, rightConvertedType: null) {
            this.kind = kind;
        }

        internal override TupleBinaryOperatorInfoKind infoKind => TupleBinaryOperatorInfoKind.NullNull;
    }
}
