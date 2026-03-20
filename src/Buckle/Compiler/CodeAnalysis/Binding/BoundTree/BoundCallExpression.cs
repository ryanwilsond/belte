using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

internal partial class BoundCallExpression {
    internal override Symbol expressionSymbol => method;

    internal bool IsConstructorInitializer() {
        return method.methodKind == MethodKind.Constructor &&
            receiver is not null &&
            (receiver.kind is BoundKind.ThisExpression or BoundKind.BaseExpression);
    }
}
