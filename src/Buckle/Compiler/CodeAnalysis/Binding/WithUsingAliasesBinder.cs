using System.Collections.Immutable;
using System.Threading;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Binding;

internal abstract partial class WithUsingAliasesBinder : Binder {
    private ImportChain _lazyImportChain;

    private protected WithUsingAliasesBinder(WithUsingNamespacesAndTypesBinder next) : base(next) { }

    internal abstract override ImmutableArray<AliasAndUsingDirective> usingAliases { get; }

    internal override ImportChain importChain {
        get {
            if (_lazyImportChain is null)
                Interlocked.CompareExchange(ref _lazyImportChain, BuildImportChain(), null);

            return _lazyImportChain;
        }
    }

    private protected abstract ImportChain BuildImportChain();

    private protected abstract ImmutableDictionary<string, AliasAndUsingDirective> GetUsingAliasesMap(
        ConsList<TypeSymbol> basesBeingResolved
    );

    internal override void LookupSymbolsInSingleBinder(
        LookupResult result,
        string name,
        int arity,
        ConsList<TypeSymbol> basesBeingResolved,
        LookupOptions options,
        Binder originalBinder,
        TextLocation errorLocation,
        bool diagnose) {
        LookupSymbolInAliases(
            GetUsingAliasesMap(basesBeingResolved),
            originalBinder,
            result,
            name,
            arity,
            errorLocation,
            basesBeingResolved,
            options,
            diagnose
        );
    }

    internal override void AddLookupSymbolsInfoInSingleBinder(
        LookupSymbolsInfo result,
        LookupOptions options,
        Binder originalBinder) {
        AddLookupSymbolsInfoInAliases(
            GetUsingAliasesMap(null),
            result,
            options,
            originalBinder
        );
    }

    internal bool IsUsingAlias(string name, ConsList<TypeSymbol> basesBeingResolved) {
        return IsUsingAlias(GetUsingAliasesMap(basesBeingResolved), name);
    }

    private protected sealed override SourceDataContainerSymbol LookupLocal(SyntaxToken nameToken) {
        return null;
    }

    private protected sealed override LocalFunctionSymbol LookupLocalFunction(SyntaxToken nameToken) {
        return null;
    }

    internal static WithUsingAliasesBinder Create(
        SourceNamespaceSymbol declaringSymbol,
        BelteSyntaxNode declarationSyntax,
        WithUsingNamespacesAndTypesBinder next) {
        return new FromSyntax(declaringSymbol, declarationSyntax, next);
    }
}
