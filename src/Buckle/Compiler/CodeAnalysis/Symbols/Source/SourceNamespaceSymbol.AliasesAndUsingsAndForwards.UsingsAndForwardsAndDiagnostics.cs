using System.Collections.Immutable;
using Buckle.CodeAnalysis.Binding;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Symbols;

internal partial class SourceNamespaceSymbol {
    private sealed partial class AliasesAndUsingsAndForwards {
        private class UsingsAndForwardsAndDiagnostics {
            internal static readonly UsingsAndForwardsAndDiagnostics Empty =
                new UsingsAndForwardsAndDiagnostics() {
                    usingAliases = [],
                    usingAliasesMap = null,
                    usingNamespacesOrTypes = [],
                    forwardAliases = [],
                    forwardAliasesMap = null,
                    forwardNamespacesOrTypes = [],
                    diagnostics = null
                };

            internal ImmutableArray<AliasAndUsingDirective> usingAliases { get; init; }

            internal ImmutableDictionary<string, AliasAndUsingDirective> usingAliasesMap { get; init; }

            internal ImmutableArray<NamespaceOrTypeAndUsingDirective> usingNamespacesOrTypes { get; init; }

            internal ImmutableArray<AliasAndForwardDirective> forwardAliases { get; init; }

            internal ImmutableDictionary<string, AliasAndForwardDirective> forwardAliasesMap { get; init; }

            internal ImmutableArray<NamespaceOrTypeAndForwardDirective> forwardNamespacesOrTypes { get; init; }

            internal BelteDiagnosticQueue diagnostics { get; init; }
        }
    }
}
