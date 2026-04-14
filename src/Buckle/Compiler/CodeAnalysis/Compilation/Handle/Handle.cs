using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Evaluating;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis;

internal sealed class Handle {
    private readonly Executor _executor;
    private readonly MethodSymbol _method;

    internal Handle(
        BoundProgram handlerProgram,
        NamedTypeSymbol handlerType,
        MethodSymbol handlerMethod,
        BelteDiagnosticQueue diagnostics) {
        _method = handlerMethod;
        _executor = Executor.CreateForHandler(handlerProgram, diagnostics, handlerType);
    }

    internal void DispatchMessage(Message message, CompilerContext context) {
        _ = _executor.ExecuteMethod(_method, [message, context]);
    }
}
