using System.Collections.Immutable;
using System.Threading;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Binding;

internal abstract partial class WithUsingAliasesBinder {
    private sealed class FromSyntax : WithUsingAliasesBinder {
        private readonly SourceNamespaceSymbol _declaringSymbol;
        private readonly BelteSyntaxNode _declarationSyntax;
        private ImmutableArray<AliasAndUsingDirective> _lazyUsingAliases;
        private ImmutableDictionary<string, AliasAndUsingDirective> _lazyUsingAliasesMap;

        internal FromSyntax(
            SourceNamespaceSymbol declaringSymbol,
            BelteSyntaxNode declarationSyntax,
            WithUsingNamespacesAndTypesBinder next)
            : base(next) {
            _declaringSymbol = declaringSymbol;
            _declarationSyntax = declarationSyntax;
        }

        internal override ImmutableArray<AliasAndUsingDirective> usingAliases {
            get {
                if (_lazyUsingAliases.IsDefault) {
                    ImmutableInterlocked.InterlockedInitialize(
                        ref _lazyUsingAliases,
                        _declaringSymbol.GetUsingAliases(_declarationSyntax, basesBeingResolved: null)
                    );
                }

                return _lazyUsingAliases;
            }
        }

        private protected override ImmutableDictionary<string, AliasAndUsingDirective> GetUsingAliasesMap(
            ConsList<TypeSymbol> basesBeingResolved) {
            if (_lazyUsingAliasesMap is null) {
                Interlocked.CompareExchange(
                    ref _lazyUsingAliasesMap,
                    _declaringSymbol.GetUsingAliasesMap(_declarationSyntax, basesBeingResolved),
                    null
                );
            }

            return _lazyUsingAliasesMap;
        }
    }
}
