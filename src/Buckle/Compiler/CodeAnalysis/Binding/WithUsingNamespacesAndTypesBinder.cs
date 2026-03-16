using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Binding;

internal abstract partial class WithUsingNamespacesAndTypesBinder : Binder {
    private protected WithUsingNamespacesAndTypesBinder(Binder next) : base(next) { }

    internal abstract ImmutableArray<NamespaceOrTypeAndUsingDirective> GetUsings(
        ConsList<TypeSymbol> basesBeingResolved
    );

    internal override void LookupSymbolsInSingleBinder(
        LookupResult result,
        string name,
        int arity,
        ConsList<TypeSymbol> basesBeingResolved,
        LookupOptions options,
        Binder originalBinder,
        bool diagnose) {

        foreach (var typeOrNamespace in GetUsings(basesBeingResolved)) {
            var candidates = GetCandidateMembers(
                typeOrNamespace.namespaceOrType,
                name,
                options,
                originalBinder: originalBinder
            );

            foreach (var symbol in candidates) {
                if (!IsValidLookupCandidateInUsings(symbol))
                    continue;

                var res = originalBinder.CheckViability(symbol, arity, options, null, diagnose, basesBeingResolved);

                // TODO Imports
                // if (res.kind == LookupResultKind.Viable) {
                //     MarkImportDirective(typeOrNamespace.UsingDirectiveReference, callerIsSemanticModel);
                // }

                result.MergeEqual(res);
            }
        }
    }

    private static bool IsValidLookupCandidateInUsings(Symbol symbol) {
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

    internal override void AddLookupSymbolsInfoInSingleBinder(LookupSymbolsInfo result, LookupOptions options, Binder originalBinder) {
        options = (options & ~(LookupOptions.NamespaceAliasesOnly | LookupOptions.NamespacesOrTypesOnly))
            | LookupOptions.MustNotBeNamespace;

        foreach (var namespaceSymbol in GetUsings(basesBeingResolved: null)) {
            foreach (var member in namespaceSymbol.namespaceOrType.GetMembersUnordered()) {
                if (IsValidLookupCandidateInUsings(member) &&
                    originalBinder.CanAddLookupSymbolInfo(member, options, result, null)) {
                    result.AddSymbol(member, member.name, member.GetArity());
                }
            }
        }
    }

    private protected override SourceDataContainerSymbol LookupLocal(SyntaxToken nameToken) {
        return null;
    }

    private protected override LocalFunctionSymbol LookupLocalFunction(SyntaxToken nameToken) {
        return null;
    }

    internal static WithUsingNamespacesAndTypesBinder Create(
        SourceNamespaceSymbol declaringSymbol,
        BelteSyntaxNode declarationSyntax,
        Binder next) {
        return new FromSyntax(declaringSymbol, declarationSyntax, next);
    }
}
