using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

internal abstract partial class WithUsingNamespacesAndTypesBinder {
    private sealed class FromNamespacesOrTypes : WithUsingNamespacesAndTypesBinder {
        private readonly ImmutableArray<NamespaceOrTypeAndUsingDirective> _usings;

        internal FromNamespacesOrTypes(
            ImmutableArray<NamespaceOrTypeAndUsingDirective> namespacesOrTypes,
            Binder next,
            bool withImportChainEntry)
            : base(next, withImportChainEntry) {
            _usings = namespacesOrTypes;
        }

        internal override ImmutableArray<NamespaceOrTypeAndUsingDirective> GetUsings(ConsList<TypeSymbol>? basesBeingResolved) {
            return _usings;
        }

        private protected override Imports GetImports() {
            return Imports.Create([], _usings);
        }
    }
}
