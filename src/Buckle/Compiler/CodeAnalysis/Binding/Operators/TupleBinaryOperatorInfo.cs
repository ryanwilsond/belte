using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

internal abstract partial class TupleBinaryOperatorInfo {
    internal readonly TypeSymbol leftConvertedType;
    internal readonly TypeSymbol rightConvertedType;

    private TupleBinaryOperatorInfo(TypeSymbol leftConvertedType, TypeSymbol rightConvertedType) {
        this.leftConvertedType = leftConvertedType;
        this.rightConvertedType = rightConvertedType;
    }

    internal abstract TupleBinaryOperatorInfoKind infoKind { get; }
}
