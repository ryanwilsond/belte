using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

internal readonly partial struct Conversion {
    private sealed class ListExpressionUncommonData : NestedUncommonData {
        internal ListExpressionUncommonData(
            ListExpressionTypeKind listExpressionTypeKind,
            TypeSymbol elementType,
            ImmutableArray<Conversion> elementConversions)
            : base(elementConversions) {
            this.listExpressionTypeKind = listExpressionTypeKind;
            this.elementType = elementType;
        }

        internal readonly ListExpressionTypeKind listExpressionTypeKind;
        internal readonly TypeSymbol elementType;
    }
}
