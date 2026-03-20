using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

internal abstract partial class WithUsingAliasesBinder {
    private sealed class FromSymbols : WithUsingAliasesBinder {
        private readonly ImmutableDictionary<string, AliasAndUsingDirective> _usingAliases;

        internal FromSymbols(
            ImmutableDictionary<string, AliasAndUsingDirective> usingAliases,
            WithUsingNamespacesAndTypesBinder next)
            : base(next) {
            _usingAliases = usingAliases;
        }

        internal override ImmutableArray<AliasAndUsingDirective> usingAliases
            => _usingAliases.SelectAsArray(static pair => pair.Value);

        private protected override ImmutableDictionary<string, AliasAndUsingDirective> GetUsingAliasesMap(
            ConsList<TypeSymbol> basesBeingResolved) {
            return _usingAliases;
        }
    }
}
