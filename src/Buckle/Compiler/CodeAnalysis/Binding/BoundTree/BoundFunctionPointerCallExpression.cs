using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

internal partial class BoundFunctionPointerCallExpression {
    public FunctionPointerTypeSymbol functionPointer => (FunctionPointerTypeSymbol)invokedExpression.type;
}
