using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;
using Buckle.Libraries;

namespace Buckle.CodeAnalysis;

internal sealed class HandleManager {
    private readonly List<DirectiveTriviaSyntax> _handlesSyntax;
    private readonly Compilation _compilation;
    private readonly CompilerContext _context;
    private MultiDictionary<int, Handle> _handles;
    private ImmutableArray<Handle> _handlesInPriorityOrder;

    internal HandleManager(Compilation compilation, DirectiveTriviaSyntax[] handles) {
        _compilation = compilation;
        _context = new CompilerContext(compilation);
        _handlesSyntax = [];
        _handlesSyntax.AddRange(handles);
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

    internal void SendDiagnosticsMessage(BelteDiagnosticQueue diagnostics) {
        DispatchMessage(new DiagnosticsMessage(diagnostics));
    }

    private void BuildHandles() {
        if (_handles is null) {
            MultiDictionary<int, Handle> builder = [];

            foreach (var syntax in _handlesSyntax) {
                if (syntax is HandleDirectiveTriviaSyntax h) {
                    var handle = CreateHandle(h, out var priority);
                    builder.Add(priority, handle);
                }
            }

            var inPriorityOrder = builder.OrderBy(k => k.Key)
                .Select(t => t.Value.ToArray())
                .SelectMany(t => t)
                .Reverse()
                .ToImmutableArray();

            Interlocked.CompareExchange(ref _handles, builder, null);
            ImmutableInterlocked.InterlockedCompareExchange(ref _handlesInPriorityOrder, inPriorityOrder, default);
        }
    }

    private Handle CreateHandle(HandleDirectiveTriviaSyntax syntax, out int priority) {
        priority = 0;

        if (syntax.priority is not null) {
            var priorityValue = syntax.priority.value;
            var priorityType = SpecialTypeExtensions.SpecialTypeFromLiteralValue(priorityValue);

            if (!LiteralUtilities.TrySpecialCastCore(priorityValue, priorityType, SpecialType.Int32, out var result)) {
                _compilation.declarationDiagnostics.Push(
                    Error.CannotConvertConstantValue(
                        syntax.priority.location,
                        priority,
                        CorLibrary.GetSpecialType(SpecialType.Int32)
                    )
                );
            } else {
                priority = (int)result;
            }
        }

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

        return new Handle(
            program,
            foundHandleClass,
            (MethodSymbol)handleCandidates[0],
            _compilation.declarationDiagnostics
        );
    }

    private void DispatchMessage(Message message) {
        foreach (var handler in _handlesInPriorityOrder)
            handler?.DispatchMessage(message, _context);
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

        if (Evaluating.Executor.ResolveType(p2) != typeof(CompilerContext))
            return false;

        return true;
    }
}
