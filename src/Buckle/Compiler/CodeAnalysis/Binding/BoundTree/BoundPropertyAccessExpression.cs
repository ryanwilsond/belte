using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

internal partial class BoundPropertyAccessExpression {
    internal override Symbol expressionSymbol => @property;
}
