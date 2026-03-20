using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

internal partial class BoundFieldAccessExpression {
    internal override Symbol expressionSymbol => @field;
}
