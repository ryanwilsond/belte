using System.Collections.Immutable;
using System.Threading;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Binding;

internal abstract partial class WithUsingNamespacesAndTypesBinder : Binder {
    private readonly bool _withImportChainEntry;
    private ImportChain _lazyImportChain;

    private protected WithUsingNamespacesAndTypesBinder(Binder next, bool withImportChainEntry) : base(next) {
        _withImportChainEntry = withImportChainEntry;
    }

    internal override ImportChain importChain {
        get {
            if (_lazyImportChain is null) {
                var importChain = next.importChain;

                if (_withImportChainEntry) {
                    importChain = new ImportChain(GetImports(), importChain);
                }

                Interlocked.CompareExchange(ref _lazyImportChain, importChain, null);
            }

            return _lazyImportChain;
        }
    }

    internal abstract ImmutableArray<NamespaceOrTypeAndUsingDirective> GetUsings(
        ConsList<TypeSymbol> basesBeingResolved
    );

    internal abstract ImmutableArray<NamespaceOrTypeAndForwardDirective> GetForwards(
        ConsList<TypeSymbol> basesBeingResolved
    );

    // Used by WithUsingAliasesBinder
    internal abstract ImmutableArray<AliasAndForwardDirective> GetForwardAliases(
        ConsList<TypeSymbol> basesBeingResolved);

    // Used by WithUsingAliasesBinder
    internal abstract ImmutableDictionary<string, AliasAndForwardDirective> GetForwardAliasesMap(
        ConsList<TypeSymbol> basesBeingResolved);

    // Used by WithUsingAliasesBinder
    internal abstract ImmutableDictionary<ForwardDirectiveSyntax, SyntaxReference> GetForwardAliasesOrigins(
        ConsList<TypeSymbol> basesBeingResolved);

    private protected abstract SyntaxReference GetForwardOrigin(ForwardDirectiveSyntax forwardDirective);

    internal override void LookupSymbolsInSingleBinder(
        LookupResult result,
        string name,
        int arity,
        ConsList<TypeSymbol> basesBeingResolved,
        LookupOptions options,
        Binder originalBinder,
        TextLocation errorLocation,
        bool diagnose) {
        foreach (var typeOrNamespace in GetUsings(basesBeingResolved)) {
            var candidates = GetCandidateMembers(
                typeOrNamespace.namespaceOrType,
                name,
                options,
                originalBinder: originalBinder
            );

            foreach (var symbol in candidates) {
                if (!IsValidLookupCandidateInUsingsOrForwards(symbol))
                    continue;

                var res = originalBinder.CheckViability(
                    symbol,
                    arity,
                    options,
                    null,
                    diagnose,
                    errorLocation,
                    basesBeingResolved
                );

                if (res.kind == LookupResultKind.Viable)
                    MarkImportDirective(typeOrNamespace.usingDirectiveReference);

                result.MergeEqual(res);
            }
        }

        foreach (var typeOrNamespace in GetForwards(basesBeingResolved)) {
            var candidates = GetCandidateMembers(
                typeOrNamespace.namespaceOrType,
                name,
                options,
                originalBinder: originalBinder
            );

            foreach (var symbol in candidates) {
                if (!IsValidLookupCandidateInUsingsOrForwards(symbol))
                    continue;

                var res = originalBinder.CheckViability(
                    symbol,
                    arity,
                    options,
                    null,
                    diagnose,
                    errorLocation,
                    basesBeingResolved
                );

                if (res.kind == LookupResultKind.Viable)
                    MarkImportDirective(typeOrNamespace.forwardDirectiveReference);

                result.MergeEqual(res);
            }
        }
    }

    private protected override void MarkImportDirective(SyntaxReference directive) {
        base.MarkImportDirective(directive);

        if (directive.node is ForwardDirectiveSyntax forwardDirective)
            base.MarkImportDirective(GetForwardOrigin(forwardDirective));
    }

    private static bool IsValidLookupCandidateInUsingsOrForwards(Symbol symbol) {
        switch (symbol.kind) {
            case SymbolKind.Namespace:
                return false;
            case SymbolKind.Method:
                if (!symbol.isStatic)
                    return false;

                break;
            case SymbolKind.NamedType:
                break;
            default:
                if (!symbol.isStatic)
                    return false;

                break;
        }

        return true;
    }

    internal override void AddLookupSymbolsInfoInSingleBinder(
        LookupSymbolsInfo result,
        LookupOptions options,
        Binder originalBinder) {
        options = (options & ~(LookupOptions.NamespaceAliasesOnly | LookupOptions.NamespacesOrTypesOnly))
            | LookupOptions.MustNotBeNamespace;

        foreach (var namespaceSymbol in GetUsings(basesBeingResolved: null)) {
            foreach (var member in namespaceSymbol.namespaceOrType.GetMembersUnordered()) {
                if (IsValidLookupCandidateInUsingsOrForwards(member) &&
                    originalBinder.CanAddLookupSymbolInfo(member, options, result, null)) {
                    result.AddSymbol(member, member.name, member.GetArity());
                }
            }
        }

        foreach (var namespaceSymbol in GetForwards(basesBeingResolved: null)) {
            foreach (var member in namespaceSymbol.namespaceOrType.GetMembersUnordered()) {
                if (IsValidLookupCandidateInUsingsOrForwards(member) &&
                    originalBinder.CanAddLookupSymbolInfo(member, options, result, null)) {
                    result.AddSymbol(member, member.name, member.GetArity());
                }
            }
        }
    }

    private protected abstract Imports GetImports();

    private protected override SourceDataContainerSymbol LookupLocal(SyntaxToken nameToken) {
        return null;
    }

    private protected override LocalFunctionSymbol LookupLocalFunction(SyntaxToken nameToken) {
        return null;
    }

    private protected override SourceTokenSymbol LookupToken(SyntaxToken identifier) {
        return null;
    }

    internal static WithUsingNamespacesAndTypesBinder Create(
        SourceNamespaceSymbol declaringSymbol,
        BelteSyntaxNode declarationSyntax,
        Binder next,
        bool withImportChainEntry = false) {
        return new FromSyntax(declaringSymbol, declarationSyntax, next, withImportChainEntry);
    }
}
