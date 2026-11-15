using System.Collections.Immutable;
using System.Threading;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Binding;

internal abstract partial class WithUsingAliasesBinder : Binder {
    private readonly WithUsingNamespacesAndTypesBinder _next;

    private ImportChain _lazyImportChain;
    private ImmutableArray<AliasAndForwardDirective> _lazyForwardedAliases;
    private ImmutableDictionary<string, AliasAndForwardDirective> _lazyForwardedAliasesMap;
    private ImmutableDictionary<ForwardDirectiveSyntax, SyntaxReference> _lazyForwardOriginMap;

    private protected WithUsingAliasesBinder(WithUsingNamespacesAndTypesBinder next) : base(next) {
        _next = next;
    }

    internal abstract override ImmutableArray<AliasAndUsingDirective> usingAliases { get; }

    internal override ImmutableArray<AliasAndForwardDirective> forwardedAliases {
        get {
            if (_lazyForwardedAliases.IsDefault) {
                ImmutableInterlocked.InterlockedInitialize(
                    ref _lazyForwardedAliases,
                    _next.GetForwardAliases(null)
                );
            }

            return _lazyForwardedAliases;
        }
    }

    private ImmutableDictionary<ForwardDirectiveSyntax, SyntaxReference> _forwardOrigins {
        get {
            if (_lazyForwardOriginMap is null) {
                Interlocked.CompareExchange(
                    ref _lazyForwardOriginMap,
                    _next.GetForwardAliasesOrigins(null),
                    null
                );
            }

            return _lazyForwardOriginMap;
        }
    }

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

    private protected ImmutableDictionary<string, AliasAndForwardDirective> GetForwardedAliasesMap(
        ConsList<TypeSymbol> basesBeingResolved) {
        if (_lazyForwardedAliasesMap is null) {
            Interlocked.CompareExchange(
                ref _lazyForwardedAliasesMap,
                _next.GetForwardAliasesMap(basesBeingResolved),
                null
            );
        }

        return _lazyForwardedAliasesMap;
    }

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
            GetForwardedAliasesMap(basesBeingResolved),
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
            GetForwardedAliasesMap(null),
            result,
            options,
            originalBinder
        );
    }

    internal bool IsAlias(string name, ConsList<TypeSymbol> basesBeingResolved) {
        return IsUsingAlias(GetUsingAliasesMap(basesBeingResolved), name) ||
            IsForwardedAlias(GetForwardedAliasesMap(basesBeingResolved), name);
    }

    private protected sealed override SourceDataContainerSymbol LookupLocal(SyntaxToken nameToken) {
        return null;
    }

    private protected sealed override LocalFunctionSymbol LookupLocalFunction(SyntaxToken nameToken) {
        return null;
    }

    private protected override SourceTokenSymbol LookupToken(SyntaxToken identifier) {
        return null;
    }

    internal static WithUsingAliasesBinder Create(
        SourceNamespaceSymbol declaringSymbol,
        BelteSyntaxNode declarationSyntax,
        WithUsingNamespacesAndTypesBinder next) {
        return new FromSyntax(declaringSymbol, declarationSyntax, next);
    }

    private protected override void MarkImportDirective(SyntaxReference directive) {
        base.MarkImportDirective(directive);

        if (directive.node is ForwardDirectiveSyntax forwardDirective)
            base.MarkImportDirective(GetForwardOrigin(forwardDirective));
    }

    private SyntaxReference GetForwardOrigin(ForwardDirectiveSyntax forwardDirective) {
        return _forwardOrigins[forwardDirective];
    }
}
