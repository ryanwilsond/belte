using System.Collections.Generic;
using System.Threading;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis;

public sealed partial class Compilation {
    internal sealed class HandleManager {
        private readonly List<DirectiveTriviaSyntax> _handlesSyntax;
        private readonly Compilation _compilation;
        private List<Handle> _handles;

        internal HandleManager(Compilation compilation, DirectiveTriviaSyntax[] handles) {
            _compilation = compilation;
            _handlesSyntax = [];
            _handlesSyntax.AddRange(handles);
        }

        internal void AddHandles(DirectiveTriviaSyntax[] handles) {
            _handlesSyntax.AddRange(handles);

            if (_handles is null)
                return;

            foreach (var handle in handles) {
                if (handle is HandleDirectiveTriviaSyntax h)
                    _handles.Add(CreateHandle(h));
            }
        }

        internal void SendParsedMessage() {
            BuildHandles();
            DispatchMessage(new Message(MessageKind.Parsed));
        }

        internal void SendBoundMessage() {
            DispatchMessage(new Message(MessageKind.Bound));
        }

        internal void SendBeforeEmitMessage() {
            DispatchMessage(new Message(MessageKind.BeforeEmit));
        }

        internal void SendFinishedMessage() {
            DispatchMessage(new Message(MessageKind.Finished));
        }

        private void BuildHandles() {
            if (_handles is null) {
                List<Handle> builder = [];

                foreach (var syntax in _handlesSyntax) {
                    if (syntax is HandleDirectiveTriviaSyntax h)
                        builder.Add(CreateHandle(h));
                }

                Interlocked.CompareExchange(ref _handles, builder, null);
            }
        }

        private Handle CreateHandle(HandleDirectiveTriviaSyntax syntax) {
            var targetName = syntax.identifier.text;
            var candidates = _compilation.globalNamespaceInternal.GetTypeMembers(targetName);

            if (candidates.Length == 0) {
                _compilation.declarationDiagnostics.Push(
                    Error.SingleTypeNameNotFound(syntax.identifier.location, targetName)
                );
                return null;
            } else if (candidates.Length > 1) {
                _compilation.declarationDiagnostics.Push(
                    Error.AmbiguousMember(syntax.identifier.location, candidates[0], candidates[1])
                );
                return null;
            }

            var foundHandleClass = candidates[0];
            var handleCandidates = foundHandleClass.GetMembers()
                .WhereAsArray(s => s is MethodSymbol m && HasHandlerSignature(m));

            if (handleCandidates.Length == 0) {
                _compilation.declarationDiagnostics.Push(
                    Error.NoHandleTarget(syntax.identifier.location, foundHandleClass)
                );
                return null;
            } else if (handleCandidates.Length > 1) {
                _compilation.declarationDiagnostics.Push(
                    Error.AmbiguousHandleTarget(syntax.identifier.location, foundHandleClass)
                );
                return null;
            }

            var foundHandleMethod = handleCandidates[0];
            var program = MethodCompiler.CompileMethodBodies(
                _compilation,
                _compilation.declarationDiagnostics,
                s => (object)s.originalDefinition == foundHandleClass.originalDefinition ||
                     (object)s.containingType?.originalDefinition == foundHandleClass.originalDefinition ||
                     (s is NamespaceSymbol)
            );

            if (_compilation.declarationDiagnostics.AnyErrors())
                return null;

            return new Handle(program, foundHandleClass, (MethodSymbol)handleCandidates[0], _compilation);
        }

        private void DispatchMessage(Message message) {
            foreach (var handler in _handles)
                handler?.DispatchMessage(message);
        }

        private bool HasHandlerSignature(MethodSymbol method) {
            if (!method.isStatic)
                return false;

            if (method.parameterCount != 2)
                return false;

            var refKinds = method.GetParameterRefKinds();

            if (refKinds != default) {
                if (refKinds[0] != RefKind.None || refKinds[1] != RefKind.None)
                    return false;
            }

            var paramTypes = method.GetParameterTypes();

            if (paramTypes[0].type.StrippedType().originalDefinition is not PENamedTypeSymbol p1 ||
                paramTypes[1].type.StrippedType().originalDefinition is not PENamedTypeSymbol p2) {
                return false;
            }

            if (Evaluating.Executor.ResolveType(p1) != typeof(Message))
                return false;

            if (Evaluating.Executor.ResolveType(p2) != typeof(Compilation))
                return false;

            return true;
        }
    }
}
