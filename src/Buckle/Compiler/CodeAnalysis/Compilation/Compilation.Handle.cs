using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Evaluating;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis;

public sealed partial class Compilation {
    private sealed class Handle {
        private readonly BoundProgram _program;
        private readonly Executor _executor;
        private readonly MethodSymbol _method;
        private readonly Compilation _compilation;

        internal Handle(
            BoundProgram handlerProgram,
            NamedTypeSymbol handlerType,
            MethodSymbol handlerMethod,
            Compilation compilation) {
            _program = handlerProgram;
            _method = handlerMethod;
            _compilation = compilation;
            _executor = Executor.CreateForHandler(handlerProgram, compilation.declarationDiagnostics, handlerType);
        }

        internal void DispatchMessage(Message message) {
            var result = _executor.ExecuteMethod(_method, [message, _compilation]);
        }
    }
}
