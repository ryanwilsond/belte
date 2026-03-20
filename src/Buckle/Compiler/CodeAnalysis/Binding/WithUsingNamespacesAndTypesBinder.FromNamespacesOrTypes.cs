using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

internal abstract partial class WithUsingNamespacesAndTypesBinder {
    private sealed class FromNamespacesOrTypes : WithUsingNamespacesAndTypesBinder {
        private readonly ImmutableArray<NamespaceOrTypeAndUsingDirective> _usings;

        internal FromNamespacesOrTypes(ImmutableArray<NamespaceOrTypeAndUsingDirective> namespacesOrTypes, Binder next)
            : base(next) {
            _usings = namespacesOrTypes;
        }

        internal override ImmutableArray<NamespaceOrTypeAndUsingDirective> GetUsings(ConsList<TypeSymbol>? basesBeingResolved) {
            return _usings;
        }
    }
}
