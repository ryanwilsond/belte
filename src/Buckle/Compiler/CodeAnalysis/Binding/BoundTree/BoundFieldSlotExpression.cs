using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

internal partial class BoundFieldSlotExpression {
    internal override Symbol expressionSymbol => @field;
}
