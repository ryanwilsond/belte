using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;

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
        LookupMembersInternal(result, container, name, arity, basesBeingResolved, options, originalBinder, diagnose);
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
