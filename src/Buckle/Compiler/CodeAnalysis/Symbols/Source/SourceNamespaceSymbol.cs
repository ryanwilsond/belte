using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Symbols;

internal partial class SourceNamespaceSymbol : NamespaceSymbol {
    private static readonly ImmutableDictionary<SingleNamespaceDeclaration, AliasesAndUsings> EmptyMap =
        ImmutableDictionary<SingleNamespaceDeclaration, AliasesAndUsings>.Empty
            .WithComparers(ReferenceEqualityComparer.Instance);

    private static readonly Func<SingleNamespaceDeclaration, SyntaxReference> DeclaringSyntaxReferencesSelector = d =>
        new NamespaceDeclarationSyntaxReference(d.syntaxReference);

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
        Symbol container,
        MergedNamespaceDeclaration mergedDeclaration,
        BelteDiagnosticQueue diagnostics) {
        containingSymbol = container;
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

    internal override NamespaceExtent extent => new NamespaceExtent();

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
                        var members = GetMembers();
                        var allCompleted = true;

                        foreach (var member in members) {
                            member.ForceComplete(location);
                            allCompleted = allCompleted && member.HasComplete(CompletionParts.All);
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
        AdditionalRegistration(builder, declaringCompilation.options);

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

    private protected virtual void AdditionalRegistration(
        PooledDictionary<ReadOnlyMemory<char>, object> builder,
        CompilationOptions options) { }

    private static void CheckMembers(
        NamespaceSymbol @namespace,
        Dictionary<ReadOnlyMemory<char>, ImmutableArray<NamespaceOrTypeSymbol>> result,
        BelteDiagnosticQueue diagnostics) {
        var memberOfArity = new Symbol[10];
        MergedNamespaceSymbol mergedAssemblyNamespace = null;

        // TODO assemblies/modules
        // if (@namespace.containingAssembly.Modules.Length > 1) {
        //     mergedAssemblyNamespace = @namespace.ContainingAssembly.GetAssemblyNamespace(@namespace) as MergedNamespaceSymbol;
        // }

        foreach (var name in result.Keys) {
            Array.Clear(memberOfArity, 0, memberOfArity.Length);
            foreach (var symbol in result[name]) {
                var nts = symbol as SourceMemberContainerTypeSymbol;

                var arity = (nts is not null) ? nts.arity : 0;

                if (arity >= memberOfArity.Length)
                    Array.Resize(ref memberOfArity, arity + 1);

                var other = memberOfArity[arity];

                if (other is null && mergedAssemblyNamespace is not null) {
                    foreach (var constituent in mergedAssemblyNamespace.constituentNamespaces) {
                        if ((object)constituent != (object)@namespace) {
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

                memberOfArity[arity] = symbol;

                // TODO If we want this error we have to deal with locals auto-privating
                // if (nts is not null) {
                //     var declaredAccessibility = nts.declaredAccessibility;

                //     if (declaredAccessibility != Accessibility.Public)
                //         diagnostics.Push(Error.NoNamespacePrivate(symbol.location));
                // }
            }
        }
    }

    private NamespaceOrTypeSymbol BuildSymbol(
        MergedNamespaceOrTypeDeclaration declaration,
        BelteDiagnosticQueue diagnostics) {
        switch (declaration.kind) {
            case DeclarationKind.Namespace:
                return new SourceNamespaceSymbol(this, (MergedNamespaceDeclaration)declaration, diagnostics);
            case DeclarationKind.Struct:
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
                    containingCompilation.RegisterDeclaredSpecialType(type);

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
}
