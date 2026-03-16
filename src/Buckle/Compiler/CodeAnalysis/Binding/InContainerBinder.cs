using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Binding;

internal class InContainerBinder : Binder {
    internal InContainerBinder(NamespaceOrTypeSymbol container, Binder next) : base(next) {
        this.container = container;
    }

    internal NamespaceOrTypeSymbol container { get; }

    internal override Symbol containingMember => container;

    internal override bool IsAccessibleHelper(
        Symbol symbol,
        TypeSymbol accessThroughType,
        out bool failedThroughTypeCheck) {
        var type = container as NamedTypeSymbol;

        if (type is not null)
            return IsSymbolAccessibleConditional(symbol, type, accessThroughType, out failedThroughTypeCheck);
        else
            return next.IsAccessibleHelper(symbol, accessThroughType, out failedThroughTypeCheck);
    }

    internal override void LookupSymbolsInSingleBinder(
        LookupResult result,
        string name,
        int arity,
        ConsList<TypeSymbol> basesBeingResolved,
        LookupOptions options,
        Binder originalBinder,
        bool diagnose) {
        if ((options & LookupOptions.NamespaceAliasesOnly) == 0) {
            LookupMembersInternal(
                result,
                container,
                name,
                arity,
                basesBeingResolved,
                options,
                originalBinder,
                diagnose
            );

            if (result.isMultiViable) {
                if (arity == 0) {
                    if (next is WithUsingAliasesBinder withUsingAliases &&
                        withUsingAliases.IsUsingAlias(name, basesBeingResolved)) {
                        var error = Error.ConflictingAliasAndMember(container.location, name, container);
                        var errorType = new ExtendedErrorTypeSymbol(
                            containingSymbol: null,
                            name,
                            arity,
                            error,
                            unreported: true
                        );

                        result.SetFrom(LookupResult.Good(errorType));
                    }
                }

                return;
            }
        }
    }

    internal override void AddLookupSymbolsInfoInSingleBinder(
        LookupSymbolsInfo result,
        LookupOptions options,
        Binder originalBinder) {
        AddMemberLookupSymbolsInfo(result, container, options, originalBinder);
    }

    private protected override SourceDataContainerSymbol LookupLocal(SyntaxToken identifier) {
        return null;
    }

    private protected override LocalFunctionSymbol LookupLocalFunction(SyntaxToken identifier) {
        return null;
    }
}
