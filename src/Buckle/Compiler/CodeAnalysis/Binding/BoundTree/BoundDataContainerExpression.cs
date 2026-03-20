using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

internal partial class BoundDataContainerExpression {
    internal override Symbol expressionSymbol => dataContainer;
}
