using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Symbols;

internal partial class SourceNamespaceSymbol {
    private sealed partial class AliasesAndUsings {
        private UsingsAndDiagnostics _lazyGlobalUsings;
        private UsingsAndDiagnostics _lazyUsings;
        private Imports _lazyImports;
        private SymbolCompletionState _state;

        internal ImmutableArray<AliasAndUsingDirective> GetUsingAliases(
            SourceNamespaceSymbol declaringSymbol,
            BelteSyntaxNode declarationSyntax,
            ConsList<TypeSymbol> basesBeingResolved) {
            return GetUsingsAndDiagnostics(declaringSymbol, declarationSyntax, basesBeingResolved).usingAliases;
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
            return GetUsingsAndDiagnostics(declaringSymbol, declarationSyntax, basesBeingResolved)
                .usingAliasesMap ?? ImmutableDictionary<string, AliasAndUsingDirective>.Empty;
        }

        internal ImmutableDictionary<string, AliasAndUsingDirective> GetGlobalUsingAliasesMap(
            SourceNamespaceSymbol declaringSymbol,
            SyntaxReference declarationSyntax,
            ConsList<TypeSymbol> basesBeingResolved) {
            return (_lazyGlobalUsings ?? GetGlobalUsingsAndDiagnostics(
                declaringSymbol,
                (BelteSyntaxNode)declarationSyntax.node,
                basesBeingResolved
            )).usingAliasesMap ?? ImmutableDictionary<string, AliasAndUsingDirective>.Empty;
        }

        internal ImmutableArray<NamespaceOrTypeAndUsingDirective> GetUsingNamespacesOrTypes(
            SourceNamespaceSymbol declaringSymbol,
            BelteSyntaxNode declarationSyntax,
            ConsList<TypeSymbol> basesBeingResolved) {
            return GetUsingsAndDiagnostics(declaringSymbol, declarationSyntax, basesBeingResolved)
                .usingNamespacesOrTypes;
        }

        private UsingsAndDiagnostics GetUsingsAndDiagnostics(
            SourceNamespaceSymbol declaringSymbol,
            BelteSyntaxNode declarationSyntax,
            ConsList<TypeSymbol> basesBeingResolved) {
            return GetUsingsAndDiagnostics(
                ref _lazyUsings,
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

        private UsingsAndDiagnostics GetGlobalUsingsAndDiagnostics(
            SourceNamespaceSymbol declaringSymbol,
            BelteSyntaxNode declarationSyntax,
            ConsList<TypeSymbol> basesBeingResolved) {
            return GetUsingsAndDiagnostics(
                ref _lazyGlobalUsings,
                declaringSymbol,
                declarationSyntax,
                basesBeingResolved,
                onlyGlobal: true
            );
        }

        private UsingsAndDiagnostics GetUsingsAndDiagnostics(
            ref UsingsAndDiagnostics usings,
            SourceNamespaceSymbol declaringSymbol,
            BelteSyntaxNode declarationSyntax,
            ConsList<TypeSymbol> basesBeingResolved,
            bool onlyGlobal) {
            if (usings is null) {
                SyntaxList<UsingDirectiveSyntax> usingDirectives;
                bool? applyIsGlobalFilter;

                switch (declarationSyntax) {
                    case CompilationUnitSyntax compilationUnit:
                        applyIsGlobalFilter = onlyGlobal;
                        usingDirectives = compilationUnit.usings;
                        break;
                    case BaseNamespaceDeclarationSyntax namespaceDecl:
                        applyIsGlobalFilter = null;
                        usingDirectives = namespaceDecl.usings;
                        break;
                    default:
                        throw ExceptionUtilities.UnexpectedValue(declarationSyntax);
                }

                UsingsAndDiagnostics result;

                if (!usingDirectives.Any()) {
                    if (applyIsGlobalFilter != false) {
                        result = UsingsAndDiagnostics.Empty;
                    } else {
                        result = new UsingsAndDiagnostics() {
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
                    result = BuildUsings(usingDirectives, declaringSymbol, declarationSyntax, applyIsGlobalFilter, basesBeingResolved);
                }

                Interlocked.CompareExchange(ref usings, result, null);
            }

            return usings;

            UsingsAndDiagnostics BuildUsings(
                SyntaxList<UsingDirectiveSyntax> usingDirectives,
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

                Binder declarationBinder = null;
                PooledHashSet<NamespaceOrTypeSymbol> uniqueUsings = null;
                PooledHashSet<NamespaceOrTypeSymbol> uniqueGlobalUsings = null;

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

                        var identifierValueText = identifier.text;
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
                        } else if (imported.kind == SymbolKind.NamedType) {
                            if (usingDirective.staticKeyword is null) {
                                diagnostics.Push(Error.BadUsingNamespace(usingDirective.namespaceOrType.location, imported));
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
                        } else if (imported.kind is SymbolKind.ArrayType) {
                            diagnostics.Push(Error.BadUsingStaticType(
                                usingDirective.namespaceOrType.location,
                                imported.kind.Localize()
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

                uniqueUsings?.Free();
                uniqueGlobalUsings?.Free();

                if (!diagnostics.Any())
                    diagnostics = null;

                return new UsingsAndDiagnostics() {
                    usingAliases = usingAliases?.ToImmutableAndFree() ?? globalUsingAliases,
                    usingAliasesMap = usingAliasesMap?.ToImmutable() ?? globalUsingAliasesMap,
                    usingNamespacesOrTypes = usings?.ToImmutableAndFree() ?? globalUsingNamespacesOrTypes,
                    diagnostics = diagnostics
                };

                static PooledHashSet<NamespaceOrTypeSymbol> GetOrCreateUniqueUsings(
                    ref PooledHashSet<NamespaceOrTypeSymbol> uniqueUsings,
                    ImmutableArray<NamespaceOrTypeAndUsingDirective> globalUsingNamespacesOrTypes) {
                    if (uniqueUsings is null) {
                        uniqueUsings = SpecializedSymbolCollections.GetPooledSymbolHashSetInstance<NamespaceOrTypeSymbol>();
                        uniqueUsings.AddAll(globalUsingNamespacesOrTypes.Select(n => n.namespaceOrType));
                    }

                    return uniqueUsings;
                }

                static PooledHashSet<NamespaceOrTypeSymbol> GetOrCreateUniqueGlobalUsingsNotInTree(
                    ref PooledHashSet<NamespaceOrTypeSymbol> uniqueUsings,
                    ImmutableArray<NamespaceOrTypeAndUsingDirective> globalUsingNamespacesOrTypes,
                    SyntaxTree tree) {
                    if (uniqueUsings is null) {
                        uniqueUsings = SpecializedSymbolCollections.GetPooledSymbolHashSetInstance<NamespaceOrTypeSymbol>();
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
                    : UsingsAndDiagnostics.Empty
                );

            var usingsAndDiagnostics = _lazyUsings ??
                GetUsingsAndDiagnostics(
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
                                    usingsAndDiagnostics,
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
            UsingsAndDiagnostics usingsAndDiagnostics,
            BelteDiagnosticQueue globalUsingDiagnostics) {
            var compilation = declaringSymbol.declaringCompilation;
            var semanticDiagnostics = compilation.declarationDiagnostics;

            var diagnostics = BelteDiagnosticQueue.GetInstance();

            if (usingsAndDiagnostics.usingAliasesMap is not null) {
                foreach (var (_, alias) in usingsAndDiagnostics.usingAliasesMap) {
                    if (alias.usingDirectiveReference.syntaxTree != declarationSyntax.syntaxTree)
                        continue;

                    var target = alias.alias.GetAliasTarget(basesBeingResolved: null);

                    diagnostics.Clear();

                    if (alias.alias is AliasSymbolFromSyntax aliasFromSyntax)
                        diagnostics.PushRange(aliasFromSyntax.aliasTargetDiagnostics);

                    alias.alias.CheckConstraints(diagnostics);

                    semanticDiagnostics.PushRange(diagnostics);
                    RecordImportDependencies(alias.usingDirective, target);
                }
            }

            foreach (var @using in usingsAndDiagnostics.usingNamespacesOrTypes) {
                if (@using.usingDirectiveReference.syntaxTree != declarationSyntax.syntaxTree)
                    continue;

                diagnostics.Clear();
                var target = @using.namespaceOrType;
                var usingDirective = @using.usingDirective;

                if (target.isType) {
                    var typeSymbol = (TypeSymbol)target;
                    var location = usingDirective.namespaceOrType.location;
                    typeSymbol.CheckAllConstraints(compilation, location, diagnostics);
                }

                semanticDiagnostics.PushRange(diagnostics);
                RecordImportDependencies(usingDirective, target);
            }

            if (usingsAndDiagnostics.diagnostics?.Any() == true)
                semanticDiagnostics.PushRange(usingsAndDiagnostics.diagnostics);

            if (globalUsingDiagnostics?.Any() == true)
                semanticDiagnostics.PushRange(globalUsingDiagnostics);

            diagnostics.Free();

            void RecordImportDependencies(UsingDirectiveSyntax usingDirective, NamespaceOrTypeSymbol target) {
                // TODO doc comments
                // if (Compilation.ReportUnusedImportsInTree(usingDirective.syntaxTree)) {
                //     compilation.RecordImportDependencies(
                //         usingDirective,
                //         diagnostics.dependenciesBag.ToImmutableArray()
                //     );
                // } else {
                if (target.isNamespace)
                    diagnostics.AddAssembliesUsedByNamespaceReference((NamespaceSymbol)target);

                compilation.AddUsedAssemblies(diagnostics.dependenciesBag);
                // }
            }
        }
    }
}
