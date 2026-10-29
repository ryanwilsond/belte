using Buckle.CodeAnalysis.Evaluating;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis;

internal sealed class Handle {
    private readonly Executor _executor;
    private readonly MethodSymbol _method;
    private readonly Compilation _compilation;

    internal Handle(
        BoundProgram handlerProgram,
        NamedTypeSymbol handlerType,
        MethodSymbol handlerMethod,
        BelteDiagnosticQueue diagnostics) {
        _method = handlerMethod;
        _compilation = handlerProgram.compilation;
        _executor = Executor.CreateForHandler(handlerProgram, diagnostics, handlerType);
    }

    internal void DispatchMessage(Message message, CompilerContext context, BelteDiagnosticQueue diagnostics) {
        _ = _executor.ExecuteMethod(
            _method,
            [message, context],
            _method.location,
            _compilation,
            diagnostics,
            out _
        );
    }
}
