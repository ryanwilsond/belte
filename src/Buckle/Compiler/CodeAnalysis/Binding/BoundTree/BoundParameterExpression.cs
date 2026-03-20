using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

internal partial class BoundParameterExpression {
    internal override Symbol expressionSymbol => parameter;
}
