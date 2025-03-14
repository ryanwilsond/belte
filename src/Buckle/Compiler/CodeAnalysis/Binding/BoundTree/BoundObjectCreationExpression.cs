using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

internal partial class BoundObjectCreationExpression {
    internal override Symbol expressionSymbol => constructor;
}
