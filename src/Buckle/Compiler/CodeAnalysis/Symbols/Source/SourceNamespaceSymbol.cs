using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
using Buckle.Libraries;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Symbols;

internal partial class SourceNamespaceSymbol : NamespaceSymbol {
    private static readonly ImmutableDictionary<SingleNamespaceDeclaration, AliasesAndUsings> EmptyMap =
        ImmutableDictionary<SingleNamespaceDeclaration, AliasesAndUsings>.Empty
            .WithComparers(ReferenceEqualityComparer.Instance);

    private static readonly Func<SingleNamespaceDeclaration, SyntaxReference> DeclaringSyntaxReferencesSelector = d =>
        new NamespaceDeclarationSyntaxReference(d.syntaxReference);

    private readonly SourceModuleSymbol _module;

    private SymbolCompletionState _state;
    private ImmutableArray<TextLocation> _locations;
    private Dictionary<ReadOnlyMemory<char>, ImmutableArray<NamespaceOrTypeSymbol>> _nameToMembersMap;
    private Dictionary<ReadOnlyMemory<char>, ImmutableArray<NamedTypeSymbol>> _nameToTypeMembersMap;
    private ImmutableArray<Symbol> _lazyAllMembers;
    private ImmutableArray<NamedTypeSymbol> _lazyTypeMembersUnordered;
    private ImmutableDictionary<SingleNamespaceDeclaration, AliasesAndUsings> _aliasesAndUsings_doNotAccessDirectly
        = EmptyMap;

    private MergedGlobalAliasesAndUsings _lazyMergedGlobalAliasesAndUsings;

    private const int LazyAllMembersIsSorted = 0x1;
    private int _flags;

    private LexicalSortKey _lazyLexicalSortKey = LexicalSortKey.NotInitialized;

    internal SourceNamespaceSymbol(
        SourceModuleSymbol module,
        Symbol container,
        MergedNamespaceDeclaration mergedDeclaration,
        BelteDiagnosticQueue diagnostics) {
        containingSymbol = container;
        _module = module;
        this.mergedDeclaration = mergedDeclaration;

        foreach (var singleDeclaration in mergedDeclaration.declarations)
            diagnostics.PushRange(singleDeclaration.diagnostics);
    }

    public override string name => mergedDeclaration.name;

    internal override ImmutableArray<TextLocation> locations {
        get {
            if (_locations.IsDefault) {
                ImmutableInterlocked.InterlockedCompareExchange(ref _locations,
                    mergedDeclaration.nameLocations,
                    default
                );
            }

            return _locations;
        }
    }

    internal override TextLocation location => locations[0];

    internal override ImmutableArray<SyntaxReference> declaringSyntaxReferences
        => ComputeDeclaringReferencesCore();

    internal override SyntaxReference syntaxReference => declaringSyntaxReferences[0];

    internal MergedNamespaceDeclaration mergedDeclaration { get; }

    internal override Symbol containingSymbol { get; }

    internal override NamespaceExtent extent => new NamespaceExtent(_module);

    internal override ModuleSymbol containingModule => _module;

    internal override AssemblySymbol containingAssembly => _module.containingAssembly;

    internal override LexicalSortKey GetLexicalSortKey() {
        if (!_lazyLexicalSortKey.isInitialized)
            _lazyLexicalSortKey.SetFrom(mergedDeclaration.GetLexicalSortKey(declaringCompilation));

        return _lazyLexicalSortKey;
    }

    internal bool HasLocationContainedWithin(
        SyntaxTree tree,
        TextSpan declarationSpan,
        out bool wasZeroWidthMatch) {
        foreach (var decl in mergedDeclaration.declarations) {
            if (IsLocationContainedWithin(decl.nameLocation, tree, declarationSpan, out wasZeroWidthMatch))
                return true;
        }

        wasZeroWidthMatch = false;
        return false;
    }

    private ImmutableArray<SyntaxReference> ComputeDeclaringReferencesCore() {
        return mergedDeclaration.declarations.SelectAsArray(DeclaringSyntaxReferencesSelector);
    }

    internal override ImmutableArray<Symbol> GetMembersUnordered() {
        var result = _lazyAllMembers;

        if (result.IsDefault) {
            var members = StaticCast<Symbol>.From(GetNameToMembersMap().Flatten(null));
            ImmutableInterlocked.InterlockedInitialize(ref _lazyAllMembers, members);
            result = _lazyAllMembers;
        }

        return result;
    }

    internal override void ForceComplete(TextLocation location) {
        while (true) {
            var incompletePart = _state.nextIncompletePart;

            switch (incompletePart) {
                case CompletionParts.NameToMembersMap:
                    _ = GetNameToMembersMap();
                    break;
                case CompletionParts.MembersCompleted: {
                        SingleNamespaceDeclaration? targetDeclarationWithImports = null;

                        foreach (var declaration in mergedDeclaration.declarations) {
                            if (location is null || location.tree == declaration.syntaxReference.syntaxTree) {
                                if (declaration.hasGlobalUsings || declaration.hasUsings || declaration.hasExternAliases) {
                                    targetDeclarationWithImports = declaration;
                                    GetAliasesAndUsings(declaration).Complete(this, declaration.syntaxReference);
                                }
                            }
                        }

                        if (isGlobalNamespace && (location is null || targetDeclarationWithImports is not null))
                            GetMergedGlobalAliasesAndUsings(basesBeingResolved: null).Complete(this);

                        var members = GetMembers();
                        var allCompleted = true;

                        if (declaringCompilation.options.concurrentBuild) {
                            Parallel.For(
                                0,
                                members.Length,
                                new ParallelOptions { MaxDegreeOfParallelism = declaringCompilation.options.maxCoreCount },
                                i => members[i].ForceComplete(location)
                            );

                            foreach (var member in members) {
                                if (!member.HasComplete(CompletionParts.All)) {
                                    allCompleted = false;
                                    break;
                                }
                            }
                        } else {
                            foreach (var member in members) {
                                member.ForceComplete(location);
                                allCompleted = allCompleted && member.HasComplete(CompletionParts.All);
                            }
                        }

                        if (allCompleted) {
                            _state.NotePartComplete(CompletionParts.MembersCompleted);
                            break;
                        } else {
                            goto done;
                        }
                    }
                case CompletionParts.None:
                    return;
                default:
                    _state.NotePartComplete(CompletionParts.All & ~CompletionParts.NamespaceSymbolAll);
                    break;
            }

            _state.SpinWaitComplete(incompletePart);
        }

done:
        _state.SpinWaitComplete(CompletionParts.NamespaceSymbolAll);
    }

    internal override bool HasComplete(CompletionParts part) {
        return _state.HasComplete(part);
    }

    internal override ImmutableArray<Symbol> GetMembers() {
        if ((_flags & LazyAllMembersIsSorted) != 0) {
            return _lazyAllMembers;
        } else {
            var allMembers = GetMembersUnordered();

            if (allMembers.Length >= 2) {
                allMembers = allMembers.Sort(LexicalOrderSymbolComparer.Instance);
                ImmutableInterlocked.InterlockedExchange(ref _lazyAllMembers, allMembers);
            }

            ThreadSafeFlagOperations.Set(ref _flags, LazyAllMembersIsSorted);
            return allMembers;
        }
    }

    internal override ImmutableArray<Symbol> GetMembers(ReadOnlyMemory<char> name) {
        return GetNameToMembersMap().TryGetValue(name, out var members)
            ? members.Cast<NamespaceOrTypeSymbol, Symbol>()
            : [];
    }

    internal override ImmutableArray<NamedTypeSymbol> GetTypeMembersUnordered() {
        if (_lazyTypeMembersUnordered.IsDefault) {
            var members = GetNameToTypeMembersMap().Flatten();
            ImmutableInterlocked.InterlockedInitialize(ref _lazyTypeMembersUnordered, members);
        }

        return _lazyTypeMembersUnordered;
    }

    internal override ImmutableArray<NamedTypeSymbol> GetTypeMembers() {
        return GetNameToTypeMembersMap().Flatten(LexicalOrderSymbolComparer.Instance);
    }

    internal override ImmutableArray<NamedTypeSymbol> GetTypeMembers(ReadOnlyMemory<char> name) {
        return GetNameToTypeMembersMap().TryGetValue(name, out var members) ? members : [];
    }

    internal override ImmutableArray<NamedTypeSymbol> GetTypeMembers(ReadOnlyMemory<char> name, int arity) {
        return GetTypeMembers(name).WhereAsArray((s, arity) => s.arity == arity, arity);
    }

    private Dictionary<ReadOnlyMemory<char>, ImmutableArray<NamespaceOrTypeSymbol>> GetNameToMembersMap() {
        if (_nameToMembersMap == null) {
            var diagnostics = BelteDiagnosticQueue.GetInstance();

            if (Interlocked.CompareExchange(ref _nameToMembersMap, MakeNameToMembersMap(diagnostics), null) is null) {
                AddDeclarationDiagnostics(diagnostics);
                _state.NotePartComplete(CompletionParts.NameToMembersMap);
            }

            diagnostics.Free();
        }

        return _nameToMembersMap;
    }

    private Dictionary<ReadOnlyMemory<char>, ImmutableArray<NamedTypeSymbol>> GetNameToTypeMembersMap() {
        if (_nameToTypeMembersMap is null) {
            Interlocked.CompareExchange(
                ref _nameToTypeMembersMap,
                ImmutableArrayExtensions
                    .GetTypesFromMemberMap<ReadOnlyMemory<char>, NamespaceOrTypeSymbol, NamedTypeSymbol>(
                        GetNameToMembersMap(),
                        ReadOnlyMemoryOfCharComparer.Instance
                    ),
                null
            );
        }

        return _nameToTypeMembersMap;
    }

    private Dictionary<ReadOnlyMemory<char>, ImmutableArray<NamespaceOrTypeSymbol>> MakeNameToMembersMap(
        BelteDiagnosticQueue diagnostics) {
        var builder = NameToObjectPool.Allocate();

        foreach (var declaration in mergedDeclaration.children) {
            var symbol = BuildSymbol(declaration, diagnostics);
            ImmutableArrayExtensions.AddToMultiValueDictionaryBuilder(builder, symbol.name.AsMemory(), symbol);
        }

        RegisterDeclaredCorTypes(builder);

        var result = new Dictionary<ReadOnlyMemory<char>, ImmutableArray<NamespaceOrTypeSymbol>>(
            builder.Count,
            ReadOnlyMemoryOfCharComparer.Instance
        );

        ImmutableArrayExtensions.CreateNameToMembersMap<
                ReadOnlyMemory<char>,
                NamespaceOrTypeSymbol,
                NamedTypeSymbol,
                NamespaceSymbol
            >(builder, result);

        builder.Free();

        CheckMembers(this, result, diagnostics);

        return result;
    }

    private static void CheckMembers(
        NamespaceSymbol @namespace,
        Dictionary<ReadOnlyMemory<char>, ImmutableArray<NamespaceOrTypeSymbol>> result,
        BelteDiagnosticQueue diagnostics) {
        var memberOfArity = new Symbol[10];
        bool reportedShadows;
        MergedNamespaceSymbol mergedAssemblyNamespace = null;

        if (@namespace.containingAssembly.modules.Length > 1) {
            mergedAssemblyNamespace = @namespace.containingAssembly.GetAssemblyNamespace(@namespace)
                as MergedNamespaceSymbol;
        }

        foreach (var name in result.Keys) {
            reportedShadows = false;
            Array.Clear(memberOfArity, 0, memberOfArity.Length);

            foreach (var symbol in result[name]) {
                var nts = symbol as SourceMemberContainerTypeSymbol;

                var arity = (nts is not null) ? nts.arity : 0;

                if (arity >= memberOfArity.Length)
                    Array.Resize(ref memberOfArity, arity + 1);

                var other = memberOfArity[arity];

                if (other is null && mergedAssemblyNamespace is not null) {
                    foreach (var constituent in mergedAssemblyNamespace.constituentNamespaces) {
                        if ((object)constituent != @namespace) {
                            var types = constituent.GetTypeMembers(symbol.name, arity);

                            if (types.Length > 0) {
                                other = types[0];
                                break;
                            }
                        }
                    }
                }

                if (other is not null) {
                    switch (nts, other) {
                        default:
                            diagnostics.Push(Error.DuplicateNameInNamespace(symbol.location, symbol.name, @namespace));
                            break;
                    }
                }

                if (symbol is SourceNamespaceSymbol ns && !reportedShadows && @namespace.isGlobalNamespace) {
                    if (ns.name == LibraryHelpers.BelteNamespace.name) {
                        diagnostics.Push(Warning.NamespaceNameShadowsBelte(ns.location, ns));
                        reportedShadows = true;
                    }
                }

                memberOfArity[arity] = symbol;

                if (nts is not null) {
                    var declaredAccessibility = nts.declaredAccessibility;

                    if (declaredAccessibility is not Accessibility.Public and not Accessibility.NotApplicable)
                        diagnostics.Push(Error.NoNamespacePrivate(symbol.location));
                }
            }
        }
    }

    private NamespaceOrTypeSymbol BuildSymbol(
        MergedNamespaceOrTypeDeclaration declaration,
        BelteDiagnosticQueue diagnostics) {
        switch (declaration.kind) {
            case DeclarationKind.Namespace:
                return new SourceNamespaceSymbol(_module, this, (MergedNamespaceDeclaration)declaration, diagnostics);
            case DeclarationKind.Struct:
            case DeclarationKind.Enum:
            case DeclarationKind.Class:
                return new SourceNamedTypeSymbol(this, (MergedTypeDeclaration)declaration, diagnostics);
            case DeclarationKind.Script:
            case DeclarationKind.Submission:
            case DeclarationKind.ImplicitClass:
                return new ImplicitNamedTypeSymbol(this, (MergedTypeDeclaration)declaration, diagnostics);
            default:
                throw ExceptionUtilities.UnexpectedValue(declaration.kind);
        }
    }

    private void RegisterDeclaredCorTypes(PooledDictionary<ReadOnlyMemory<char>, object> members) {
        if (declaringCompilation.keepLookingForCorTypes) {
            foreach (var member in members.Values) {
                if (member is NamedTypeSymbol type && type.specialType != SpecialType.None) {
                    declaringCompilation.RegisterDeclaredSpecialType(type);

                    if (!declaringCompilation.keepLookingForCorTypes)
                        return;
                }
            }
        }
    }

    internal bool IsDefinedInSourceTree(SyntaxTree tree, TextSpan definedWithinSpan) {
        if (isGlobalNamespace)
            return true;

        foreach (var declaration in mergedDeclaration.declarations) {
            var declarationSyntaxRef = declaration.syntaxReference;

            if (declarationSyntaxRef.syntaxTree != tree)
                continue;

            if (definedWithinSpan is null)
                return true;

            var syntax = NamespaceDeclarationSyntaxReference.GetSyntax(declarationSyntaxRef);

            if (syntax.fullSpan.IntersectsWith(definedWithinSpan))
                return true;
        }

        return false;
    }

    #region Imports

    internal Imports GetImports(BelteSyntaxNode declarationSyntax, ConsList<TypeSymbol> basesBeingResolved) {
        switch (declarationSyntax) {
            case CompilationUnitSyntax compilationUnit:
                if (!compilationUnit.usings.Any())
                    return GetGlobalUsingImports(basesBeingResolved);

                break;

            case BaseNamespaceDeclarationSyntax namespaceDecl:
                if (!namespaceDecl.usings.Any())
                    return Imports.Empty;

                break;
            default:
                throw ExceptionUtilities.UnexpectedValue(declarationSyntax);
        }

        return GetAliasesAndUsings(declarationSyntax)
            .GetImports(this, declarationSyntax, basesBeingResolved);
    }

    private AliasesAndUsings GetAliasesAndUsings(BelteSyntaxNode declarationSyntax) {
        return GetAliasesAndUsings(GetMatchingNamespaceDeclaration(declarationSyntax));
    }

    private SingleNamespaceDeclaration GetMatchingNamespaceDeclaration(BelteSyntaxNode declarationSyntax) {
        foreach (var declaration in mergedDeclaration.declarations) {
            var declarationSyntaxRef = declaration.syntaxReference;

            if (declarationSyntaxRef.syntaxTree != declarationSyntax.syntaxTree)
                continue;

            if (declarationSyntaxRef.node == declarationSyntax)
                return declaration;
        }

        throw ExceptionUtilities.Unreachable();
    }

    private static AliasesAndUsings GetOrCreateAliasAndUsings(
        ref ImmutableDictionary<SingleNamespaceDeclaration, AliasesAndUsings> dictionary,
        SingleNamespaceDeclaration declaration) {
        return ImmutableInterlocked.GetOrAdd(
            ref dictionary,
            declaration,
            static _ => new AliasesAndUsings());
    }

    private AliasesAndUsings GetAliasesAndUsings(SingleNamespaceDeclaration declaration)
        => GetOrCreateAliasAndUsings(ref _aliasesAndUsings_doNotAccessDirectly, declaration);

    internal ImmutableArray<AliasAndUsingDirective> GetUsingAliases(
        BelteSyntaxNode declarationSyntax,
        ConsList<TypeSymbol> basesBeingResolved) {
        switch (declarationSyntax) {
            case CompilationUnitSyntax compilationUnit:
                if (!compilationUnit.usings.Any())
                    return [];

                break;
            case BaseNamespaceDeclarationSyntax namespaceDecl:
                if (!namespaceDecl.usings.Any())
                    return [];

                break;
            default:
                throw ExceptionUtilities.UnexpectedValue(declarationSyntax);
        }

        return GetAliasesAndUsings(declarationSyntax).GetUsingAliases(this, declarationSyntax, basesBeingResolved);
    }

    internal ImmutableDictionary<string, AliasAndUsingDirective> GetUsingAliasesMap(
        BelteSyntaxNode declarationSyntax,
        ConsList<TypeSymbol> basesBeingResolved) {
        switch (declarationSyntax) {
            case CompilationUnitSyntax compilationUnit:
                if (!compilationUnit.usings.Any())
                    return GetGlobalUsingAliasesMap(basesBeingResolved);

                break;
            case BaseNamespaceDeclarationSyntax namespaceDecl:
                if (!namespaceDecl.usings.Any())
                    return ImmutableDictionary<string, AliasAndUsingDirective>.Empty;

                break;
            default:
                throw ExceptionUtilities.UnexpectedValue(declarationSyntax);
        }

        return GetAliasesAndUsings(declarationSyntax).GetUsingAliasesMap(this, declarationSyntax, basesBeingResolved);
    }

    internal ImmutableArray<NamespaceOrTypeAndUsingDirective> GetUsingNamespacesOrTypes(
        BelteSyntaxNode declarationSyntax,
        ConsList<TypeSymbol> basesBeingResolved) {
        switch (declarationSyntax) {
            case CompilationUnitSyntax compilationUnit:
                if (!compilationUnit.usings.Any())
                    return GetGlobalUsingNamespacesOrTypes(basesBeingResolved);

                break;
            case BaseNamespaceDeclarationSyntax namespaceDecl:
                if (!namespaceDecl.usings.Any())
                    return [];

                break;
            default:
                throw ExceptionUtilities.UnexpectedValue(declarationSyntax);
        }

        return GetAliasesAndUsings(declarationSyntax)
            .GetUsingNamespacesOrTypes(this, declarationSyntax, basesBeingResolved);
    }

    private Imports GetGlobalUsingImports(ConsList<TypeSymbol> basesBeingResolved) {
        return GetMergedGlobalAliasesAndUsings(basesBeingResolved).imports;
    }

    private ImmutableDictionary<string, AliasAndUsingDirective> GetGlobalUsingAliasesMap(
        ConsList<TypeSymbol> basesBeingResolved) {
        return GetMergedGlobalAliasesAndUsings(basesBeingResolved).usingAliasesMap;
    }

    private ImmutableArray<NamespaceOrTypeAndUsingDirective> GetGlobalUsingNamespacesOrTypes(
        ConsList<TypeSymbol> basesBeingResolved) {
        return GetMergedGlobalAliasesAndUsings(basesBeingResolved).usingNamespacesOrTypes;
    }

    private MergedGlobalAliasesAndUsings GetMergedGlobalAliasesAndUsings(ConsList<TypeSymbol> basesBeingResolved) {
        if (_lazyMergedGlobalAliasesAndUsings is null) {
            if (!isGlobalNamespace) {
                _lazyMergedGlobalAliasesAndUsings = MergedGlobalAliasesAndUsings.Empty;
            } else {
                ImmutableDictionary<string, AliasAndUsingDirective>? mergedAliases = null;
                var mergedNamespacesOrTypes = ArrayBuilder<NamespaceOrTypeAndUsingDirective>.GetInstance();
                var uniqueUsings = SpecializedSymbolCollections.GetPooledSymbolHashSetInstance<NamespaceOrTypeSymbol>();
                var diagnostics = BelteDiagnosticQueue.GetInstance();

                try {
                    foreach (var singleDeclaration in mergedDeclaration.declarations) {
                        if (singleDeclaration.hasGlobalUsings) {
                            var aliases = GetAliasesAndUsings(singleDeclaration)
                                .GetGlobalUsingAliasesMap(this, singleDeclaration.syntaxReference, basesBeingResolved);

                            if (!aliases.IsEmpty) {
                                if (mergedAliases is null) {
                                    mergedAliases = aliases;
                                } else {
                                    var builder = mergedAliases.ToBuilder();
                                    var added = false;

                                    foreach (var pair in aliases) {
                                        if (builder.ContainsKey(pair.Key)) {
                                            diagnostics.Push(Error.DuplicateAlias(pair.Value.alias.location, pair.Key));
                                        } else {
                                            builder.Add(pair);
                                            added = true;
                                        }
                                    }

                                    if (added)
                                        mergedAliases = builder.ToImmutable();
                                }
                            }

                            var namespacesOrTypes = GetAliasesAndUsings(singleDeclaration)
                                .GetGlobalUsingNamespacesOrTypes(
                                    this,
                                    singleDeclaration.syntaxReference,
                                    basesBeingResolved
                                );

                            if (!namespacesOrTypes.IsEmpty) {
                                if (mergedNamespacesOrTypes.Count == 0) {
                                    mergedNamespacesOrTypes.AddRange(namespacesOrTypes);
                                    uniqueUsings.AddAll(namespacesOrTypes.Select(n => n.namespaceOrType));
                                } else {
                                    foreach (var namespaceOrType in namespacesOrTypes) {
                                        if (!uniqueUsings.Add(namespaceOrType.namespaceOrType)) {
                                            diagnostics.Push(Error.DuplicateWithGlobalUsing(
                                                namespaceOrType.usingDirective.namespaceOrType.location,
                                                namespaceOrType.namespaceOrType
                                            ));
                                        } else {
                                            mergedNamespacesOrTypes.Add(namespaceOrType);
                                        }
                                    }
                                }
                            }
                        }
                    }

                    Interlocked.CompareExchange(ref _lazyMergedGlobalAliasesAndUsings,
                        new MergedGlobalAliasesAndUsings() {
                            usingAliasesMap = mergedAliases ?? ImmutableDictionary<string, AliasAndUsingDirective>.Empty,
                            usingNamespacesOrTypes = mergedNamespacesOrTypes.ToImmutableAndFree(),
                            diagnostics = diagnostics.ToImmutableAndFree()
                        },
                        null
                    );

                    mergedNamespacesOrTypes = null;
                    diagnostics = null;
                } finally {
                    uniqueUsings.Free();
                    mergedNamespacesOrTypes?.Free();
                    diagnostics?.Free();
                }
            }
        }

        return _lazyMergedGlobalAliasesAndUsings;
    }

    #endregion
}
