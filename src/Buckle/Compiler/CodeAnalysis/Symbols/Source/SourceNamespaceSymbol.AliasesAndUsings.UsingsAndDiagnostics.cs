using System.Collections.Immutable;
using Buckle.CodeAnalysis.Binding;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Symbols;

internal partial class SourceNamespaceSymbol {
    private sealed partial class AliasesAndUsings {
        private class UsingsAndDiagnostics {
            internal static readonly UsingsAndDiagnostics Empty =
                new UsingsAndDiagnostics() {
                    usingAliases = [],
                    usingAliasesMap = null,
                    usingNamespacesOrTypes = [],
                    diagnostics = null
                };

            internal ImmutableArray<AliasAndUsingDirective> usingAliases { get; init; }

            internal ImmutableDictionary<string, AliasAndUsingDirective> usingAliasesMap { get; init; }

            internal ImmutableArray<NamespaceOrTypeAndUsingDirective> usingNamespacesOrTypes { get; init; }

            internal BelteDiagnosticQueue diagnostics { get; init; }
        }
    }
}
