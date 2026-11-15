using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Symbols;

internal partial class SourceNamespaceSymbol {
    private sealed partial class AliasesAndUsingsAndForwards {
        private UsingsAndForwardsAndDiagnostics _lazyGlobalUsings;
        private UsingsAndForwardsAndDiagnostics _lazyUsingsAndForwards;
        private Imports _lazyImports;
        private SymbolCompletionState _state;

        internal ImmutableArray<AliasAndUsingDirective> GetUsingAliases(
            SourceNamespaceSymbol declaringSymbol,
            BelteSyntaxNode declarationSyntax,
            ConsList<TypeSymbol> basesBeingResolved) {
            return GetUsingsAndForwardsAndDiagnostics(declaringSymbol, declarationSyntax, basesBeingResolved)
                .usingAliases;
        }

        internal ImmutableArray<AliasAndUsingDirective> GetGlobalUsingAliases(
            SourceNamespaceSymbol declaringSymbol,
            BelteSyntaxNode declarationSyntax,
            ConsList<TypeSymbol> basesBeingResolved) {
            return GetGlobalUsingsAndDiagnostics(declaringSymbol, declarationSyntax, basesBeingResolved).usingAliases;
        }

        internal ImmutableDictionary<string, AliasAndUsingDirective> GetUsingAliasesMap(
            SourceNamespaceSymbol declaringSymbol,
            BelteSyntaxNode declarationSyntax,
            ConsList<TypeSymbol> basesBeingResolved) {
            return GetUsingsAndForwardsAndDiagnostics(declaringSymbol, declarationSyntax, basesBeingResolved)
                .usingAliasesMap ?? [];
        }

        internal ImmutableDictionary<string, AliasAndUsingDirective> GetGlobalUsingAliasesMap(
            SourceNamespaceSymbol declaringSymbol,
            SyntaxReference declarationSyntax,
            ConsList<TypeSymbol> basesBeingResolved) {
            return (_lazyGlobalUsings ?? GetGlobalUsingsAndDiagnostics(
                declaringSymbol,
                (BelteSyntaxNode)declarationSyntax.node,
                basesBeingResolved
            )).usingAliasesMap ?? [];
        }

        internal ImmutableArray<NamespaceOrTypeAndUsingDirective> GetUsingNamespacesOrTypes(
            SourceNamespaceSymbol declaringSymbol,
            BelteSyntaxNode declarationSyntax,
            ConsList<TypeSymbol> basesBeingResolved) {
            return GetUsingsAndForwardsAndDiagnostics(declaringSymbol, declarationSyntax, basesBeingResolved)
                .usingNamespacesOrTypes;
        }

        internal ImmutableArray<AliasAndForwardDirective> GetForwardAliases(
            SourceNamespaceSymbol declaringSymbol,
            BelteSyntaxNode declarationSyntax,
            ConsList<TypeSymbol> basesBeingResolved) {
            return GetUsingsAndForwardsAndDiagnostics(declaringSymbol, declarationSyntax, basesBeingResolved)
                .forwardAliases;
        }

        internal ImmutableDictionary<string, AliasAndForwardDirective> GetForwardAliasesMap(
            SourceNamespaceSymbol declaringSymbol,
            BelteSyntaxNode declarationSyntax,
            ConsList<TypeSymbol> basesBeingResolved) {
            return GetUsingsAndForwardsAndDiagnostics(declaringSymbol, declarationSyntax, basesBeingResolved)
                .forwardAliasesMap ?? [];
        }

        internal ImmutableArray<NamespaceOrTypeAndForwardDirective> GetForwardNamespacesOrTypes(
            SourceNamespaceSymbol declaringSymbol,
            BelteSyntaxNode declarationSyntax,
            ConsList<TypeSymbol> basesBeingResolved) {
            return GetUsingsAndForwardsAndDiagnostics(declaringSymbol, declarationSyntax, basesBeingResolved)
                .forwardNamespacesOrTypes;
        }

        private UsingsAndForwardsAndDiagnostics GetUsingsAndForwardsAndDiagnostics(
            SourceNamespaceSymbol declaringSymbol,
            BelteSyntaxNode declarationSyntax,
            ConsList<TypeSymbol> basesBeingResolved) {
            return GetUsingsAndForwardsAndDiagnostics(
                ref _lazyUsingsAndForwards,
                declaringSymbol,
                declarationSyntax,
                basesBeingResolved,
                onlyGlobal: false
            );
        }

        internal ImmutableArray<NamespaceOrTypeAndUsingDirective> GetGlobalUsingNamespacesOrTypes(
            SourceNamespaceSymbol declaringSymbol,
            SyntaxReference declarationSyntax,
            ConsList<TypeSymbol> basesBeingResolved) {
            return (_lazyGlobalUsings ?? GetGlobalUsingsAndDiagnostics(
                declaringSymbol,
                (BelteSyntaxNode)declarationSyntax.node,
                basesBeingResolved
            )).usingNamespacesOrTypes;
        }

        private UsingsAndForwardsAndDiagnostics GetGlobalUsingsAndDiagnostics(
            SourceNamespaceSymbol declaringSymbol,
            BelteSyntaxNode declarationSyntax,
            ConsList<TypeSymbol> basesBeingResolved) {
            return GetUsingsAndForwardsAndDiagnostics(
                ref _lazyGlobalUsings,
                declaringSymbol,
                declarationSyntax,
                basesBeingResolved,
                onlyGlobal: true
            );
        }

        private UsingsAndForwardsAndDiagnostics GetUsingsAndForwardsAndDiagnostics(
            ref UsingsAndForwardsAndDiagnostics usingsAndForwards,
            SourceNamespaceSymbol declaringSymbol,
            BelteSyntaxNode declarationSyntax,
            ConsList<TypeSymbol> basesBeingResolved,
            bool onlyGlobal) {
            if (usingsAndForwards is null) {
                IEnumerable<UsingDirectiveSyntax> usingDirectives;
                IEnumerable<ForwardDirectiveSyntax> forwardDirectives;
                bool? applyIsGlobalFilter;

                switch (declarationSyntax) {
                    case CompilationUnitSyntax compilationUnit:
                        applyIsGlobalFilter = onlyGlobal;
                        usingDirectives = compilationUnit.elements
                            .Where(e => e is UsingDirectiveSyntax)
                            .Select(e => (UsingDirectiveSyntax)e);
                        forwardDirectives = compilationUnit.elements
                            .Where(e => e is ForwardDirectiveSyntax)
                            .Select(e => (ForwardDirectiveSyntax)e);

                        break;
                    case BaseNamespaceDeclarationSyntax namespaceDecl:
                        applyIsGlobalFilter = null;
                        usingDirectives = namespaceDecl.elements
                            .Where(e => e is UsingDirectiveSyntax)
                            .Select(e => (UsingDirectiveSyntax)e);
                        forwardDirectives = namespaceDecl.elements
                            .Where(e => e is ForwardDirectiveSyntax)
                            .Select(e => (ForwardDirectiveSyntax)e);

                        break;
                    default:
                        throw ExceptionUtilities.UnexpectedValue(declarationSyntax);
                }

                UsingsAndForwardsAndDiagnostics result;

                if (!usingDirectives.Any() && !forwardDirectives.Any()) {
                    if (applyIsGlobalFilter != false) {
                        result = UsingsAndForwardsAndDiagnostics.Empty;
                    } else {
                        result = new UsingsAndForwardsAndDiagnostics() {
                            usingAliases = GetGlobalUsingAliases(
                                declaringSymbol,
                                declarationSyntax,
                                basesBeingResolved
                            ),
                            usingAliasesMap = declaringSymbol.GetGlobalUsingAliasesMap(basesBeingResolved),
                            usingNamespacesOrTypes = declaringSymbol.GetGlobalUsingNamespacesOrTypes(
                                basesBeingResolved
                            ),
                            diagnostics = null
                        };
                    }
                } else {
                    result = BuildUsingsAndForwards(
                        usingDirectives,
                        forwardDirectives,
                        declaringSymbol,
                        declarationSyntax,
                        applyIsGlobalFilter,
                        basesBeingResolved
                    );
                }

                Interlocked.CompareExchange(ref usingsAndForwards, result, null);
            }

            return usingsAndForwards;

            UsingsAndForwardsAndDiagnostics BuildUsingsAndForwards(
                IEnumerable<UsingDirectiveSyntax> usingDirectives,
                IEnumerable<ForwardDirectiveSyntax> forwardDirectives,
                SourceNamespaceSymbol declaringSymbol,
                BelteSyntaxNode declarationSyntax,
                bool? applyIsGlobalFilter,
                ConsList<TypeSymbol> basesBeingResolved) {
                var globalUsingAliasesMap = ImmutableDictionary<string, AliasAndUsingDirective>.Empty;
                var globalUsingNamespacesOrTypes = ImmutableArray<NamespaceOrTypeAndUsingDirective>.Empty;
                var globalUsingAliases = ImmutableArray<AliasAndUsingDirective>.Empty;

                if (applyIsGlobalFilter == false) {
                    globalUsingAliasesMap = declaringSymbol.GetGlobalUsingAliasesMap(basesBeingResolved);
                    globalUsingNamespacesOrTypes = declaringSymbol.GetGlobalUsingNamespacesOrTypes(basesBeingResolved);
                    globalUsingAliases = GetGlobalUsingAliases(declaringSymbol, declarationSyntax, basesBeingResolved);
                }

                var diagnostics = new BelteDiagnosticQueue();
                var compilation = declaringSymbol.declaringCompilation;

                ArrayBuilder<NamespaceOrTypeAndUsingDirective> usings = null;
                ImmutableDictionary<string, AliasAndUsingDirective>.Builder usingAliasesMap = null;
                ArrayBuilder<AliasAndUsingDirective> usingAliases = null;

                ArrayBuilder<NamespaceOrTypeAndForwardDirective> forwards = null;
                ImmutableDictionary<string, AliasAndForwardDirective>.Builder forwardAliasesMap = null;
                ArrayBuilder<AliasAndForwardDirective> forwardAliases = null;

                Binder declarationBinder = null;
                PooledHashSet<NamespaceOrTypeSymbol> uniqueUsings = null;
                PooledHashSet<NamespaceOrTypeSymbol> uniqueGlobalUsings = null;
                PooledHashSet<NamespaceOrTypeSymbol> uniqueForwards = null;

                if (applyIsGlobalFilter == true)
                    Debug.Assert(!forwardDirectives.Any());

                // TODO Could possibly deduplicate this code, but not sure if it is worth it

                foreach (var usingDirective in usingDirectives) {
                    if (applyIsGlobalFilter.HasValue &&
                        usingDirective.globalKeyword is not null != applyIsGlobalFilter.GetValueOrDefault()) {
                        continue;
                    }

                    compilation.RecordImport(usingDirective);

                    if (usingDirective.alias is not null) {
                        var identifier = usingDirective.alias.name.identifier;
                        var location = usingDirective.alias.name.location;

                        // TODO Reachable?
                        // if (identifier.ContextualKind() == SyntaxKind.GlobalKeyword) {
                        //     diagnostics.Add(ErrorCode.WRN_GlobalAliasDefn, location);
                        // }

                        if (usingDirective.staticKeyword is not null)
                            diagnostics.Push(Error.NoAliasHere(location));

                        var identifierValueText = identifier.valueText;
                        var skipInLookup = false;

                        if (usingAliasesMap?.ContainsKey(identifierValueText) ??
                            globalUsingAliasesMap.ContainsKey(identifierValueText)) {
                            skipInLookup = true;

                            if (!usingDirective.namespaceOrType.isFabricated)
                                diagnostics.Push(Error.DuplicateAlias(location, identifierValueText));
                        }

                        var aliasAndDirective = new AliasAndUsingDirective(
                            new AliasSymbolFromSyntax(declaringSymbol, usingDirective),
                            usingDirective
                        );

                        if (usingAliases is null) {
                            usingAliases = ArrayBuilder<AliasAndUsingDirective>.GetInstance();
                            usingAliases.AddRange(globalUsingAliases);
                        }

                        usingAliases.Add(aliasAndDirective);

                        if (!skipInLookup) {
                            usingAliasesMap ??= globalUsingAliasesMap.ToBuilder();
                            usingAliasesMap.Add(identifierValueText, aliasAndDirective);
                        }
                    } else {
                        if (usingDirective.namespaceOrType.isFabricated)
                            continue;

                        var flags = BinderFlags.SuppressConstraintChecks;
                        var directiveDiagnostics = BelteDiagnosticQueue.GetInstance();

                        declarationBinder ??= compilation.GetBinderFactory(declarationSyntax.syntaxTree)
                            .GetBinder(usingDirective.namespaceOrType)
                            .WithAdditionalFlags(flags);

                        var imported = declarationBinder.BindNamespaceOrTypeSymbol(
                            usingDirective.namespaceOrType,
                            directiveDiagnostics,
                            basesBeingResolved
                        ).namespaceOrTypeSymbol;

                        var addDirectiveDiagnostics = true;

                        if (imported.kind == SymbolKind.Namespace) {
                            if (usingDirective.staticKeyword is not null) {
                                diagnostics.Push(Error.BadUsingType(usingDirective.namespaceOrType.location, imported));
                            } else if (!GetOrCreateUniqueUsings(ref uniqueUsings, globalUsingNamespacesOrTypes)
                                .Add(imported)) {
                                var globalError = !globalUsingNamespacesOrTypes.IsEmpty &&
                                    GetOrCreateUniqueGlobalUsingsNotInTree(
                                        ref uniqueGlobalUsings,
                                        globalUsingNamespacesOrTypes,
                                        declarationSyntax.syntaxTree)
                                    .Contains(imported);

                                diagnostics.Push(
                                    globalError
                                        ? Error.DuplicateWithGlobalUsing(
                                            usingDirective.namespaceOrType.location,
                                            imported
                                        )
                                        : Error.DuplicateUsing(usingDirective.namespaceOrType.location, imported)
                                );
                            } else {
                                GetOrCreateUsingsBuilder(ref usings, globalUsingNamespacesOrTypes).Add(new NamespaceOrTypeAndUsingDirective(imported, usingDirective, dependencies: default));
                            }
                        } else if (imported is NamedTypeSymbol t && !t.IsStructType()) {
                            if (usingDirective.staticKeyword is null) {
                                diagnostics.Push(Error.BadUsingNamespace(
                                    usingDirective.namespaceOrType.location,
                                    imported.StrippedTypeOrSelf()
                                ));
                            } else {
                                var importedType = (NamedTypeSymbol)imported;

                                if (!GetOrCreateUniqueUsings(ref uniqueUsings, globalUsingNamespacesOrTypes)
                                    .Add(importedType)) {
                                    var globalError = !globalUsingNamespacesOrTypes.IsEmpty &&
                                        GetOrCreateUniqueGlobalUsingsNotInTree(
                                            ref uniqueGlobalUsings,
                                            globalUsingNamespacesOrTypes,
                                            declarationSyntax.syntaxTree
                                        ).Contains(imported);

                                    diagnostics.Push(
                                        globalError
                                            ? Error.DuplicateWithGlobalUsing(
                                                usingDirective.namespaceOrType.location,
                                                importedType
                                            )
                                            : Error.DuplicateUsing(
                                                usingDirective.namespaceOrType.location,
                                                importedType
                                            )
                                    );
                                } else {
                                    GetOrCreateUsingsBuilder(ref usings, globalUsingNamespacesOrTypes)
                                        .Add(new NamespaceOrTypeAndUsingDirective(
                                            importedType,
                                            usingDirective,
                                            // TODO
                                            // directiveDiagnostics.DependenciesBag.ToImmutableArray()
                                            []
                                        )
                                    );
                                }
                            }
                        } else if (imported.kind == SymbolKind.ArrayType ||
                            (imported is NamedTypeSymbol t2 && t2.IsStructType())) {
                            diagnostics.Push(Error.BadUsingStaticType(
                                usingDirective.namespaceOrType.location,
                                imported.kind == SymbolKind.ArrayType ? imported.kind.Localize() : "struct"
                            ));

                            addDirectiveDiagnostics = false;
                        } else if (imported.kind != SymbolKind.ErrorType) {
                            diagnostics.Push(Error.BadSKKnown(
                                usingDirective.namespaceOrType.location,
                                usingDirective.namespaceOrType,
                                imported.kind.Localize(),
                                MessageID.IDS_SK_TYPE_OR_NAMESPACE.Localize()
                            ));
                        }

                        if (addDirectiveDiagnostics)
                            diagnostics.PushRange(directiveDiagnostics);

                        directiveDiagnostics.Free();
                    }
                }

                foreach (var forwardDirective in forwardDirectives) {
                    if (forwardDirective.alias is not null) {
                        var identifier = forwardDirective.alias.name.identifier;
                        var location = forwardDirective.alias.name.location;

                        if (forwardDirective.staticKeyword is not null)
                            diagnostics.Push(Error.NoAliasHereForward(location));

                        var identifierValueText = identifier.valueText;
                        var skipInLookup = false;

                        if ((forwardAliasesMap?.ContainsKey(identifierValueText) ?? false) ||
                                (usingAliasesMap?.ContainsKey(identifierValueText) ??
                                 globalUsingAliasesMap.ContainsKey(identifierValueText))) {
                            skipInLookup = true;

                            if (!forwardDirective.namespaceOrType.isFabricated)
                                diagnostics.Push(Error.DuplicateAlias(location, identifierValueText));
                        }

                        var aliasAndDirective = new AliasAndForwardDirective(
                            new AliasSymbolFromSyntax(declaringSymbol, forwardDirective),
                            forwardDirective
                        );

                        forwardAliases ??= ArrayBuilder<AliasAndForwardDirective>.GetInstance();
                        forwardAliases.Add(aliasAndDirective);

                        if (!skipInLookup) {
                            forwardAliasesMap ??= ImmutableDictionary.CreateBuilder<string, AliasAndForwardDirective>();
                            forwardAliasesMap.Add(identifierValueText, aliasAndDirective);
                        }
                    } else {
                        if (forwardDirective.namespaceOrType.isFabricated)
                            continue;

                        var flags = BinderFlags.SuppressConstraintChecks;
                        var directiveDiagnostics = BelteDiagnosticQueue.GetInstance();

                        declarationBinder ??= compilation.GetBinderFactory(declarationSyntax.syntaxTree)
                            .GetBinder(forwardDirective.namespaceOrType)
                            .WithAdditionalFlags(flags);

                        var imported = declarationBinder.BindNamespaceOrTypeSymbol(
                            forwardDirective.namespaceOrType,
                            directiveDiagnostics,
                            basesBeingResolved
                        ).namespaceOrTypeSymbol;

                        var addDirectiveDiagnostics = true;

                        if (imported.kind == SymbolKind.Namespace) {
                            if (forwardDirective.staticKeyword is not null) {
                                diagnostics.Push(
                                    Error.BadUsingType(forwardDirective.namespaceOrType.location, imported)
                                );
                            } else if (!GetOrCreateUniqueForwards(ref uniqueForwards).Add(imported)) {
                                diagnostics.Push(
                                    Error.DuplicateForward(forwardDirective.namespaceOrType.location, imported)
                                );
                            } else {
                                GetOrCreateForwardsBuilder(ref forwards).Add(new NamespaceOrTypeAndForwardDirective(
                                    imported,
                                    forwardDirective,
                                    dependencies: default
                                ));
                            }
                        } else if (imported is NamedTypeSymbol t && !t.IsStructType()) {
                            if (forwardDirective.staticKeyword is null) {
                                diagnostics.Push(Error.BadForwardNamespace(
                                    forwardDirective.namespaceOrType.location,
                                    imported.StrippedTypeOrSelf()
                                ));
                            } else {
                                var importedType = (NamedTypeSymbol)imported;

                                if (!GetOrCreateUniqueForwards(ref uniqueForwards).Add(importedType)) {
                                    diagnostics.Push(
                                        Error.DuplicateForward(
                                            forwardDirective.namespaceOrType.location,
                                            importedType
                                        )
                                    );
                                } else {
                                    GetOrCreateForwardsBuilder(ref forwards)
                                        .Add(new NamespaceOrTypeAndForwardDirective(
                                            importedType,
                                            forwardDirective,
                                            // TODO
                                            // directiveDiagnostics.DependenciesBag.ToImmutableArray()
                                            []
                                        )
                                    );
                                }
                            }
                        } else if (imported.kind == SymbolKind.ArrayType ||
                            (imported is NamedTypeSymbol t2 && t2.IsStructType())) {
                            diagnostics.Push(Error.BadForwardStaticType(
                                forwardDirective.namespaceOrType.location,
                                imported.kind == SymbolKind.ArrayType ? imported.kind.Localize() : "struct"
                            ));

                            addDirectiveDiagnostics = false;
                        } else if (imported.kind != SymbolKind.ErrorType) {
                            diagnostics.Push(Error.BadSKKnown(
                                forwardDirective.namespaceOrType.location,
                                forwardDirective.namespaceOrType,
                                imported.kind.Localize(),
                                MessageID.IDS_SK_TYPE_OR_NAMESPACE.Localize()
                            ));
                        }

                        if (addDirectiveDiagnostics)
                            diagnostics.PushRange(directiveDiagnostics);

                        directiveDiagnostics.Free();
                    }
                }

                uniqueUsings?.Free();
                uniqueGlobalUsings?.Free();
                uniqueForwards?.Free();

                if (!diagnostics.Any())
                    diagnostics = null;

                return new UsingsAndForwardsAndDiagnostics() {
                    usingAliases = usingAliases?.ToImmutableAndFree() ?? globalUsingAliases,
                    usingAliasesMap = usingAliasesMap?.ToImmutable() ?? globalUsingAliasesMap,
                    usingNamespacesOrTypes = usings?.ToImmutableAndFree() ?? globalUsingNamespacesOrTypes,
                    forwardAliases = forwardAliases?.ToImmutableAndFree() ?? [],
                    forwardAliasesMap = forwardAliasesMap?.ToImmutable() ?? [],
                    forwardNamespacesOrTypes = forwards?.ToImmutableAndFree() ?? [],
                    diagnostics = diagnostics
                };

                static PooledHashSet<NamespaceOrTypeSymbol> GetOrCreateUniqueUsings(
                    ref PooledHashSet<NamespaceOrTypeSymbol> uniqueUsings,
                    ImmutableArray<NamespaceOrTypeAndUsingDirective> globalUsingNamespacesOrTypes) {
                    if (uniqueUsings is null) {
                        uniqueUsings = SpecializedSymbolCollections
                            .GetPooledSymbolHashSetInstance<NamespaceOrTypeSymbol>();
                        uniqueUsings.AddAll(globalUsingNamespacesOrTypes.Select(n => n.namespaceOrType));
                    }

                    return uniqueUsings;
                }

                static PooledHashSet<NamespaceOrTypeSymbol> GetOrCreateUniqueGlobalUsingsNotInTree(
                    ref PooledHashSet<NamespaceOrTypeSymbol> uniqueUsings,
                    ImmutableArray<NamespaceOrTypeAndUsingDirective> globalUsingNamespacesOrTypes,
                    SyntaxTree tree) {
                    if (uniqueUsings is null) {
                        uniqueUsings = SpecializedSymbolCollections
                            .GetPooledSymbolHashSetInstance<NamespaceOrTypeSymbol>();
                        uniqueUsings.AddAll(globalUsingNamespacesOrTypes
                            .Where(n => n.usingDirectiveReference?.syntaxTree != tree)
                            .Select(n => n.namespaceOrType));
                    }

                    return uniqueUsings;
                }

                static ArrayBuilder<NamespaceOrTypeAndUsingDirective> GetOrCreateUsingsBuilder(
                    ref ArrayBuilder<NamespaceOrTypeAndUsingDirective> usings,
                    ImmutableArray<NamespaceOrTypeAndUsingDirective> globalUsingNamespacesOrTypes) {
                    if (usings is null) {
                        usings = ArrayBuilder<NamespaceOrTypeAndUsingDirective>.GetInstance();
                        usings.AddRange(globalUsingNamespacesOrTypes);
                    }

                    return usings;
                }

                static PooledHashSet<NamespaceOrTypeSymbol> GetOrCreateUniqueForwards(
                    ref PooledHashSet<NamespaceOrTypeSymbol> uniqueForwards) {
                    uniqueForwards ??= SpecializedSymbolCollections
                        .GetPooledSymbolHashSetInstance<NamespaceOrTypeSymbol>();

                    return uniqueForwards;
                }

                static ArrayBuilder<NamespaceOrTypeAndForwardDirective> GetOrCreateForwardsBuilder(
                    ref ArrayBuilder<NamespaceOrTypeAndForwardDirective> forwards) {
                    forwards ??= ArrayBuilder<NamespaceOrTypeAndForwardDirective>.GetInstance();
                    return forwards;
                }
            }
        }

        internal Imports GetImports(
            SourceNamespaceSymbol declaringSymbol,
            BelteSyntaxNode declarationSyntax,
            ConsList<TypeSymbol> basesBeingResolved) {
            if (_lazyImports is null) {
                Interlocked.CompareExchange(
                    ref _lazyImports,
                    Imports.Create(
                        GetUsingAliasesMap(declaringSymbol, declarationSyntax, basesBeingResolved),
                        GetUsingNamespacesOrTypes(declaringSymbol, declarationSyntax, basesBeingResolved)
                    ),
                    null
                );
            }

            return _lazyImports;
        }

        internal void Complete(SourceNamespaceSymbol declaringSymbol, SyntaxReference declarationSyntax) {
            var globalUsingsAndDiagnostics = _lazyGlobalUsings ??
                (declaringSymbol.isGlobalNamespace
                    ? GetGlobalUsingsAndDiagnostics(
                        declaringSymbol,
                        (BelteSyntaxNode)declarationSyntax.node,
                        basesBeingResolved: null
                    )
                    : UsingsAndForwardsAndDiagnostics.Empty
                );

            var usingsAndForwardsAndDiagnostics = _lazyUsingsAndForwards ??
                GetUsingsAndForwardsAndDiagnostics(
                    declaringSymbol,
                    (BelteSyntaxNode)declarationSyntax.node,
                    basesBeingResolved: null
                );

            while (true) {
                var incompletePart = _state.nextIncompletePart;

                switch (incompletePart) {
                    case CompletionParts.StartValidatingImports: {
                            if (_state.NotePartComplete(CompletionParts.StartValidatingImports)) {
                                Validate(
                                    declaringSymbol,
                                    declarationSyntax,
                                    usingsAndForwardsAndDiagnostics,
                                    globalUsingsAndDiagnostics.diagnostics
                                );

                                _state.NotePartComplete(CompletionParts.FinishValidatingImports);
                            }
                        }
                        break;

                    case CompletionParts.FinishValidatingImports:
                        _state.SpinWaitComplete(CompletionParts.FinishValidatingImports);
                        break;
                    case CompletionParts.None:
                        return;
                    default:
                        _state.NotePartComplete(CompletionParts.All & ~CompletionParts.ImportsAll);
                        break;
                }

                _state.SpinWaitComplete(incompletePart);
            }
        }

        private static void Validate(
            SourceNamespaceSymbol declaringSymbol,
            SyntaxReference declarationSyntax,
            UsingsAndForwardsAndDiagnostics usingsAndForwardsAndDiagnostics,
            BelteDiagnosticQueue globalUsingDiagnostics) {
            var compilation = declaringSymbol.declaringCompilation;
            var semanticDiagnostics = compilation.declarationDiagnostics;

            var diagnostics = BelteDiagnosticQueue.GetInstance();

            if (usingsAndForwardsAndDiagnostics.usingAliasesMap is { } usingAliasesMap) {
                foreach (var (_, alias) in usingAliasesMap) {
                    if (alias.usingDirectiveReference.syntaxTree != declarationSyntax.syntaxTree)
                        continue;

                    var target = alias.alias.GetAliasTarget(basesBeingResolved: null);

                    diagnostics.Clear();

                    if (alias.alias is AliasSymbolFromSyntax aliasFromSyntax)
                        diagnostics.PushRange(aliasFromSyntax.aliasTargetDiagnostics);

                    if (target is MethodSymbol m && m.requiresInstanceReceiver)
                        diagnostics.Push(Error.AliasMethodMustBeStatic(alias.usingDirectiveReference.location, m));

                    alias.alias.CheckConstraints(diagnostics);

                    semanticDiagnostics.PushRange(diagnostics);
                    RecordImportDependencies(target);
                }
            }

            if (usingsAndForwardsAndDiagnostics.forwardAliasesMap is { } forwardAliasesMap) {
                foreach (var (_, alias) in forwardAliasesMap) {
                    if (alias.forwardDirectiveReference.syntaxTree != declarationSyntax.syntaxTree)
                        continue;

                    var target = alias.alias.GetAliasTarget(basesBeingResolved: null);

                    diagnostics.Clear();

                    if (alias.alias is AliasSymbolFromSyntax aliasFromSyntax)
                        diagnostics.PushRange(aliasFromSyntax.aliasTargetDiagnostics);

                    if (target is MethodSymbol m && m.requiresInstanceReceiver)
                        diagnostics.Push(Error.AliasMethodMustBeStatic(alias.forwardDirectiveReference.location, m));

                    alias.alias.CheckConstraints(diagnostics);

                    semanticDiagnostics.PushRange(diagnostics);
                    RecordImportDependencies(target);
                }
            }

            var conversions = compilation.assembly.corAssemblies[0].typeConversions;

            foreach (var @using in usingsAndForwardsAndDiagnostics.usingNamespacesOrTypes) {
                if (@using.usingDirectiveReference.syntaxTree != declarationSyntax.syntaxTree)
                    continue;

                diagnostics.Clear();
                var target = @using.namespaceOrType;
                var usingDirective = @using.usingDirective;

                if (target.isType) {
                    var typeSymbol = (TypeSymbol)target;
                    var location = usingDirective.namespaceOrType.location;
                    typeSymbol.CheckAllConstraints(conversions, location, [], diagnostics);
                }

                semanticDiagnostics.PushRange(diagnostics);
                RecordImportDependencies(target);
            }

            foreach (var forward in usingsAndForwardsAndDiagnostics.forwardNamespacesOrTypes) {
                if (forward.forwardDirectiveReference.syntaxTree != declarationSyntax.syntaxTree)
                    continue;

                diagnostics.Clear();
                var target = forward.namespaceOrType;
                var forwardDirective = forward.forwardDirective;

                if (target.isType) {
                    var typeSymbol = (TypeSymbol)target;
                    var location = forwardDirective.namespaceOrType.location;
                    typeSymbol.CheckAllConstraints(conversions, location, [], diagnostics);
                } else {
                    var namespaceSymbol = (NamespaceSymbol)target;

                    foreach (var member in declaringSymbol.GetMembers()) {
                        if (member == namespaceSymbol) {
                            diagnostics.Push(Warning.UnnecessaryForward(
                                forward.forwardDirective.location,
                                namespaceSymbol,
                                declaringSymbol
                            ));

                            break;
                        }
                    }
                }

                semanticDiagnostics.PushRange(diagnostics);
                RecordImportDependencies(target);
            }

            if (usingsAndForwardsAndDiagnostics.diagnostics?.Any() == true)
                semanticDiagnostics.PushRange(usingsAndForwardsAndDiagnostics.diagnostics);

            if (globalUsingDiagnostics?.Any() == true)
                semanticDiagnostics.PushRange(globalUsingDiagnostics);

            diagnostics.Free();

            void RecordImportDependencies(Symbol target) {
                // TODO doc comments
                // if (Compilation.ReportUnusedImportsInTree(usingDirective.syntaxTree)) {
                //     compilation.RecordImportDependencies(
                //         usingDirective,
                //         diagnostics.dependenciesBag.ToImmutableArray()
                //     );
                // } else {
                if (target is NamespaceSymbol namespaceSymbol)
                    diagnostics.AddAssembliesUsedByNamespaceReference(namespaceSymbol);

                compilation.AddUsedAssemblies(diagnostics.dependenciesBag);
                // }
            }
        }
    }
}
