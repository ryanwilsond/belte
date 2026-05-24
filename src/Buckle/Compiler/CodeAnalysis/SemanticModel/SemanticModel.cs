using System;
using System.Collections.Immutable;
using System.Threading;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
using Buckle.Utilities;
using Diagnostics;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis;

public abstract class SemanticModel {
    /*
    public Compilation compilation => _compilationCore;

    private protected abstract Compilation _compilationCore { get; }

    public SyntaxTree syntaxTree => _syntaxTreeCore;

    private protected abstract SyntaxTree _syntaxTreeCore { get; }

    public IOperation? GetOperation(SyntaxNode node, CancellationToken cancellationToken = default) {
        return GetOperationCore(node, cancellationToken);
    }

    private protected abstract IOperation GetOperationCore(SyntaxNode node, CancellationToken cancellationToken);

    public virtual bool ignoresAccessibility => false;

    internal SymbolInfo GetSymbolInfo(SyntaxNode node, CancellationToken cancellationToken = default) {
        return GetSymbolInfoCore(node, cancellationToken);
    }

    private protected abstract SymbolInfo GetSymbolInfoCore(SyntaxNode node, CancellationToken cancellationToken = default);

    internal SymbolInfo GetSpeculativeSymbolInfo(
        int position,
        SyntaxNode expression,
        SpeculativeBindingOption bindingOption) {
        return GetSpeculativeSymbolInfoCore(position, expression, bindingOption);
    }

    private protected abstract SymbolInfo GetSpeculativeSymbolInfoCore(
        int position,
        SyntaxNode expression,
        SpeculativeBindingOption bindingOption);

    internal TypeInfo GetSpeculativeTypeInfo(int position, SyntaxNode expression, SpeculativeBindingOption bindingOption) {
        return GetSpeculativeTypeInfoCore(position, expression, bindingOption);
    }

    private protected abstract TypeInfo GetSpeculativeTypeInfoCore(
        int position,
        SyntaxNode expression,
        SpeculativeBindingOption bindingOption);

    internal TypeInfo GetTypeInfo(SyntaxNode node, CancellationToken cancellationToken = default) {
        return GetTypeInfoCore(node, cancellationToken);
    }

    private protected abstract TypeInfo GetTypeInfoCore(SyntaxNode node, CancellationToken cancellationToken = default);

    internal IAliasSymbol GetAliasInfo(SyntaxNode nameSyntax, CancellationToken cancellationToken = default) {
        return GetAliasInfoCore(nameSyntax, cancellationToken);
    }

    private protected abstract IAliasSymbol GetAliasInfoCore(
        SyntaxNode nameSyntax,
        CancellationToken cancellationToken = default);

    public abstract bool isSpeculativeSemanticModel { get; }

    public abstract int originalPositionForSpeculation { get; }

    public SemanticModel parentModel => _parentModelCore;

    private protected abstract SemanticModel _parentModelCore { get; }

    internal abstract SemanticModel containingPublicModelOrSelf { get; }

    internal IAliasSymbol GetSpeculativeAliasInfo(
        int position,
        SyntaxNode nameSyntax,
        SpeculativeBindingOption bindingOption) {
        return GetSpeculativeAliasInfoCore(position, nameSyntax, bindingOption);
    }

    private protected abstract IAliasSymbol GetSpeculativeAliasInfoCore(
        int position,
        SyntaxNode nameSyntax,
        SpeculativeBindingOption bindingOption);

    public abstract ImmutableArray<BelteDiagnostic> GetSyntaxDiagnostics(
        TextSpan span = null,
        CancellationToken cancellationToken = default);

    public abstract ImmutableArray<BelteDiagnostic> GetDeclarationDiagnostics(
        TextSpan span = null,
        CancellationToken cancellationToken = default);

    public abstract ImmutableArray<BelteDiagnostic> GetMethodBodyDiagnostics(
        TextSpan span = null,
        CancellationToken cancellationToken = default);

    public abstract ImmutableArray<Diagnostic> GetDiagnostics(
        TextSpan span = null,
        CancellationToken cancellationToken = default);

    internal ISymbol GetDeclaredSymbolForNode(SyntaxNode declaration, CancellationToken cancellationToken = default) {
        return GetDeclaredSymbolCore(declaration, cancellationToken);
    }

    private protected abstract ISymbol GetDeclaredSymbolCore(
        SyntaxNode declaration,
        CancellationToken cancellationToken = default);

    internal ImmutableArray<ISymbol> GetDeclaredSymbolsForNode(
        SyntaxNode declaration,
        CancellationToken cancellationToken = default) {
        return GetDeclaredSymbolsCore(declaration, cancellationToken);
    }

    private protected abstract ImmutableArray<ISymbol> GetDeclaredSymbolsCore(
        SyntaxNode declaration,
        CancellationToken cancellationToken = default);

    public ImmutableArray<ISymbol> LookupSymbols(
        int position,
        INamespaceOrTypeSymbol container = null,
        string name = null,
        bool includeReducedExtensionMethods = false) {
        return LookupSymbolsCore(position, container, name, includeReducedExtensionMethods);
    }

    private protected abstract ImmutableArray<ISymbol> LookupSymbolsCore(
        int position,
        INamespaceOrTypeSymbol container,
        string name,
        bool includeReducedExtensionMethods);

    public ImmutableArray<ISymbol> LookupBaseMembers(int position, string name = null) {
        return LookupBaseMembersCore(position, name);
    }

    private protected abstract ImmutableArray<ISymbol> LookupBaseMembersCore(int position, string name);

    public ImmutableArray<ISymbol> LookupStaticMembers(
        int position,
        INamespaceOrTypeSymbol container = null,
        string name = null) {
        return LookupStaticMembersCore(position, container, name);
    }

    private protected abstract ImmutableArray<ISymbol> LookupStaticMembersCore(
        int position,
        INamespaceOrTypeSymbol container,
        string name);

    public ImmutableArray<ISymbol> LookupNamespacesAndTypes(
        int position,
        INamespaceOrTypeSymbol container = null,
        string name = null) {
        return LookupNamespacesAndTypesCore(position, container, name);
    }

    private protected abstract ImmutableArray<ISymbol> LookupNamespacesAndTypesCore(
        int position,
        INamespaceOrTypeSymbol container,
        string name);

    public ImmutableArray<ISymbol> LookupLabels(
        int position,
        string name = null) {
        return LookupLabelsCore(position, name);
    }

    private protected abstract ImmutableArray<ISymbol> LookupLabelsCore(int position, string name);

    public Optional<object> GetConstantValue(SyntaxNode node, CancellationToken cancellationToken = default) {
        return GetConstantValueCore(node, cancellationToken);
    }

    private protected abstract Optional<object> GetConstantValueCore(
        SyntaxNode node,
        CancellationToken cancellationToken = default);

    internal ImmutableArray<ISymbol> GetMemberGroup(SyntaxNode node, CancellationToken cancellationToken = default) {
        return GetMemberGroupCore(node, cancellationToken);
    }

    private protected abstract ImmutableArray<ISymbol> GetMemberGroupCore(
        SyntaxNode node,
        CancellationToken cancellationToken = default);

    public ISymbol GetEnclosingSymbol(int position, CancellationToken cancellationToken = default) {
        return GetEnclosingSymbolCore(position, cancellationToken);
    }

    private protected abstract ISymbol GetEnclosingSymbolCore(
        int position,
        CancellationToken cancellationToken = default);

    public bool IsAccessible(int position, ISymbol symbol) {
        return IsAccessibleCore(position, symbol);
    }

    private protected abstract bool IsAccessibleCore(int position, ISymbol symbol);

    public PreprocessingSymbolInfo GetPreprocessingSymbolInfo(SyntaxNode nameSyntax) {
        return GetPreprocessingSymbolInfoCore(nameSyntax);
    }

    private protected abstract PreprocessingSymbolInfo GetPreprocessingSymbolInfoCore(SyntaxNode nameSyntax);

    internal abstract void ComputeDeclarationsInSpan(
        TextSpan span,
        bool getSymbol,
        ArrayBuilder<DeclarationInfo> builder,
        CancellationToken cancellationToken);

    internal abstract void ComputeDeclarationsInNode(
        SyntaxNode node,
        ISymbol associatedSymbol,
        bool getSymbol,
        ArrayBuilder<DeclarationInfo> builder,
        CancellationToken cancellationToken,
        int? levelsToCompute = null);

    internal virtual Func<SyntaxNode, bool> GetSyntaxNodesToAnalyzeFilter(
        SyntaxNode declaredNode,
        ISymbol declaredSymbol) {
        return null;
    }

    internal virtual bool ShouldSkipSyntaxNodeAnalysis(SyntaxNode node, ISymbol containingSymbol) {
        return false;
    }

    protected internal virtual SyntaxNode GetTopmostNodeForDiagnosticAnalysis(
        ISymbol symbol,
        SyntaxNode declaringSyntax) {
        return declaringSyntax;
    }

    internal SyntaxNode root => _rootCore;

    private protected abstract SyntaxNode _rootCore { get; }
    */
}
