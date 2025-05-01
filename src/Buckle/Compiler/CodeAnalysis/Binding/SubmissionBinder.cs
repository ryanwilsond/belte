using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// This binder acts as both an InContainerBinder and LocalScopeBinder.
/// It retrieves locals from previous submissions and contains everything in the script class.
/// Because the SubmissionBinder is never chained, it is responsible for retrieving the locals of
/// every previous submission.
/// </summary>
internal sealed class SubmissionBinder : LocalScopeBinder {
    private readonly CompilationUnitSyntax _declarationSyntax;

    internal SubmissionBinder(
        NamespaceSymbol globalNamespace,
        CompilationUnitSyntax declarationSyntax,
        Binder next) : base(next) {
        containingMember = globalNamespace;
        _declarationSyntax = declarationSyntax;
    }

    internal override Symbol containingMember { get; }

    internal override void LookupSymbolsInSingleBinder(
        LookupResult result,
        string name,
        int arity,
        ConsList<TypeSymbol> basesBeingResolved,
        LookupOptions options,
        Binder originalBinder,
        bool diagnose) {
        LookupMembersInSubmissions(
            result,
            _declarationSyntax,
            name,
            arity,
            basesBeingResolved,
            options,
            originalBinder,
            diagnose
        );

        base.LookupSymbolsInSingleBinder(result, name, arity, basesBeingResolved, options, originalBinder, diagnose);
    }

    internal override void AddLookupSymbolsInfoInSingleBinder(
        LookupSymbolsInfo result,
        LookupOptions options,
        Binder originalBinder) {
        AddMemberLookupSymbolsInfoInSubmissions(result, options, originalBinder);
    }

    private protected override ImmutableArray<DataContainerSymbol> BuildLocals() {
        var builder = ArrayBuilder<DataContainerSymbol>.GetInstance();
        return builder.ToImmutableAndFree();
    }

    private protected override ImmutableArray<LocalFunctionSymbol> BuildLocalFunctions() {
        var builder = ArrayBuilder<LocalFunctionSymbol>.GetInstance();
        return builder.ToImmutableAndFree();
    }
}
