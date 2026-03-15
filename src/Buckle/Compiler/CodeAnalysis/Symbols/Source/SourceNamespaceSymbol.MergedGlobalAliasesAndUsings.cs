using System.Collections.Immutable;
using System.Threading;
using Buckle.CodeAnalysis.Binding;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Symbols;

internal partial class SourceNamespaceSymbol {
    private class MergedGlobalAliasesAndUsings {
        private Imports _lazyImports;
        private SymbolCompletionState _state;

        internal static readonly MergedGlobalAliasesAndUsings Empty =
            new MergedGlobalAliasesAndUsings() {
                usingAliasesMap = ImmutableDictionary<string, AliasAndUsingDirective>.Empty,
                usingNamespacesOrTypes = [],
                diagnostics = [],
                _lazyImports = Imports.Empty
            };

        internal ImmutableDictionary<string, AliasAndUsingDirective> usingAliasesMap { get; init; }

        internal ImmutableArray<NamespaceOrTypeAndUsingDirective> usingNamespacesOrTypes { get; init; }

        internal ImmutableArray<BelteDiagnostic> diagnostics { get; init; }

        internal Imports imports {
            get {
                if (_lazyImports is null) {
                    Interlocked.CompareExchange(
                        ref _lazyImports,
                        Imports.Create(
                            usingAliasesMap ?? ImmutableDictionary<string, AliasAndUsingDirective>.Empty,
                            usingNamespacesOrTypes
                        ),
                        null
                    );
                }

                return _lazyImports;
            }
        }

        internal void Complete(SourceNamespaceSymbol declaringSymbol) {
            while (true) {
                var incompletePart = _state.nextIncompletePart;

                switch (incompletePart) {
                    case CompletionParts.StartValidatingImports: {
                            if (_state.NotePartComplete(CompletionParts.StartValidatingImports)) {
                                if (!diagnostics.IsDefaultOrEmpty) {
                                    var compilation = declaringSymbol.declaringCompilation;
                                    var semanticDiagnostics = compilation.declarationDiagnostics;
                                    semanticDiagnostics.PushRange(diagnostics);
                                }

                                _state.NotePartComplete(CompletionParts.FinishValidatingImports);
                            }
                        }
                        break;
                    case CompletionParts.FinishValidatingImports:
                        _state.SpinWaitComplete(CompletionParts.FinishValidatingImports);
                        break;
                    case CompletionParts.None:
                        return;
                    default:
                        _state.NotePartComplete(CompletionParts.All & ~CompletionParts.ImportsAll);
                        break;
                }

                _state.SpinWaitComplete(incompletePart);
            }
        }
    }
}
