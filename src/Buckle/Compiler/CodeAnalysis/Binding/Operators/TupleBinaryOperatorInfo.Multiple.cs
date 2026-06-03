using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

internal abstract partial class TupleBinaryOperatorInfo {
    internal class Multiple : TupleBinaryOperatorInfo {
        internal readonly ImmutableArray<TupleBinaryOperatorInfo> operators;

        internal static readonly Multiple ErrorInstance = new Multiple(
            operators: [],
            leftConvertedType: null,
            rightConvertedType: null);

        internal Multiple(
            ImmutableArray<TupleBinaryOperatorInfo> operators,
            TypeSymbol leftConvertedType,
            TypeSymbol rightConvertedType)
            : base(leftConvertedType, rightConvertedType) {
            this.operators = operators;
        }

        internal override TupleBinaryOperatorInfoKind infoKind => TupleBinaryOperatorInfoKind.Multiple;
    }
}
