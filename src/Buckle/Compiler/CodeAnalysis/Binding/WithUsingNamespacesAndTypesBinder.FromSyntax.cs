using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Binding;

internal abstract partial class WithUsingNamespacesAndTypesBinder {
    private sealed class FromSyntax : WithUsingNamespacesAndTypesBinder {
        private readonly SourceNamespaceSymbol _declaringSymbol;
        private readonly BelteSyntaxNode _declarationSyntax;
        private ImmutableArray<NamespaceOrTypeAndUsingDirective> _lazyUsings;

        internal FromSyntax(
            SourceNamespaceSymbol declaringSymbol,
            BelteSyntaxNode declarationSyntax,
            Binder next,
            bool withImportChainEntry)
            : base(next, withImportChainEntry) {
            _declaringSymbol = declaringSymbol;
            _declarationSyntax = declarationSyntax;
        }

        internal override ImmutableArray<NamespaceOrTypeAndUsingDirective> GetUsings(
            ConsList<TypeSymbol> basesBeingResolved) {
            if (_lazyUsings.IsDefault) {
                ImmutableInterlocked.InterlockedInitialize(
                    ref _lazyUsings,
                    _declaringSymbol.GetUsingNamespacesOrTypes(_declarationSyntax, basesBeingResolved)
                );
            }

            return _lazyUsings;
        }

        private protected override Imports GetImports() {
            return _declaringSymbol.GetImports(_declarationSyntax, basesBeingResolved: null);
        }
    }
}
