using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

internal partial class BoundCallExpression {
    internal override Symbol expressionSymbol => method;
}
