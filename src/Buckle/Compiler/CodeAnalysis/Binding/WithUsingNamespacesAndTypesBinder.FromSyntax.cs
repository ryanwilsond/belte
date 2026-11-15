using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Threading;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Binding;

internal abstract partial class WithUsingNamespacesAndTypesBinder {
    private sealed class FromSyntax : WithUsingNamespacesAndTypesBinder {
        private readonly SourceNamespaceSymbol _declaringSymbol;
        private readonly BelteSyntaxNode _declarationSyntax;
        private ImmutableArray<NamespaceOrTypeAndUsingDirective> _lazyUsings;
        private ImmutableArray<NamespaceOrTypeAndForwardDirective> _lazyForwards;
        private ImmutableArray<AliasAndForwardDirective> _lazyForwardAliases;
        private ImmutableArray<AliasAndForwardDirective> _lazyForwardUnresolvedAliases;
        private ImmutableDictionary<ForwardDirectiveSyntax, SyntaxReference> _lazyForwardOrigins;
        private ConcurrentDictionary<AliasAndForwardDirective, ForwardDirectiveSyntax> _lazyUnresolvedForwardsSyntax;
        private ConcurrentSet<AliasAndForwardDirective> _lazyForwardsNeedingMapping;

        internal FromSyntax(
            SourceNamespaceSymbol declaringSymbol,
            BelteSyntaxNode declarationSyntax,
            Binder next,
            bool withImportChainEntry)
            : base(next, withImportChainEntry) {
            _declaringSymbol = declaringSymbol;
            _declarationSyntax = declarationSyntax;
        }

        internal override ImmutableArray<NamespaceOrTypeAndUsingDirective> GetUsings(
            ConsList<TypeSymbol> basesBeingResolved) {
            if (_lazyUsings.IsDefault) {
                ImmutableInterlocked.InterlockedInitialize(
                    ref _lazyUsings,
                    _declaringSymbol.GetUsingNamespacesOrTypes(_declarationSyntax, basesBeingResolved)
                );
            }

            return _lazyUsings;
        }

        internal override ImmutableArray<NamespaceOrTypeAndForwardDirective> GetForwards(
            ConsList<TypeSymbol> basesBeingResolved) {
            if (_lazyForwards.IsDefault) {
                var usings = GetUsings(basesBeingResolved);
                var builder = ArrayBuilder<NamespaceOrTypeAndForwardDirective>.GetInstance();
                var originBuilder = ImmutableDictionary.CreateBuilder<ForwardDirectiveSyntax, SyntaxReference>();

                foreach (var namespaceOrType in usings) {
                    if (!namespaceOrType.namespaceOrType.isNamespace)
                        continue;

                    var forwards = ((NamespaceSymbol)namespaceOrType.namespaceOrType)
                        .GetForwardNamespacesOrTypes(basesBeingResolved);

                    foreach (var forward in forwards) {
                        if (forward.unresolvedTarget is not null) {
                            var name = SyntaxFactory.ParseName(forward.unresolvedTarget);
                            var tempDiagnostics = BelteDiagnosticQueue.GetInstance();
                            var symbol = BindNamespaceOrTypeSymbol(name, tempDiagnostics, basesBeingResolved)
                                .namespaceOrTypeSymbol;

                            if (forward.isUnresolvedType != symbol.isType || tempDiagnostics.AnyErrors()) {
                                tempDiagnostics.Free();
                                continue;
                            }

                            tempDiagnostics.Free();

                            var syntax = SyntaxFactory.ForwardDirective(
                                SyntaxFactory.Token(SyntaxKind.ForwardKeyword),
                                name,
                                SyntaxFactory.Token(SyntaxKind.SemicolonToken)
                            );

                            builder.Add(new NamespaceOrTypeAndForwardDirective(symbol, syntax, []));
                            originBuilder.Add(syntax, namespaceOrType.usingDirectiveReference);
                        } else {
                            builder.Add(forward);
                            originBuilder.Add(forward.forwardDirective, namespaceOrType.usingDirectiveReference);
                        }
                    }
                }

                ImmutableInterlocked.InterlockedInitialize(
                    ref _lazyForwards,
                    builder.ToImmutableAndFree()
                );

                Interlocked.CompareExchange(
                    ref _lazyForwardOrigins,
                    originBuilder.ToImmutable(),
                    null
                );
            }

            return _lazyForwards;
        }

        internal override ImmutableArray<AliasAndForwardDirective> GetForwardAliases(
            ConsList<TypeSymbol> basesBeingResolved) {
            GetForwardAliasesUnresolvedOnly(basesBeingResolved);

            if (_lazyForwardAliases.IsDefault) {
                var usings = GetUsings(basesBeingResolved);
                var builder = ArrayBuilder<AliasAndForwardDirective>.GetInstance();

                foreach (var namespaceOrType in usings) {
                    if (!namespaceOrType.namespaceOrType.isNamespace)
                        continue;

                    var forwards = ((NamespaceSymbol)namespaceOrType.namespaceOrType)
                        .GetForwardAliases(basesBeingResolved);

                    foreach (var forward in forwards) {
                        if (forward.unresolvedTarget is null)
                            builder.Add(forward);
                    }
                }

                builder.AddRange(_lazyForwardUnresolvedAliases);

                ImmutableInterlocked.InterlockedInitialize(
                    ref _lazyForwardAliases,
                    builder.ToImmutableAndFree()
                );
            }

            return _lazyForwardAliases;
        }

        private void GetForwardAliasesUnresolvedOnly(
            ConsList<TypeSymbol> basesBeingResolved) {
            if (_lazyForwardUnresolvedAliases.IsDefault) {
                var usings = GetUsings(basesBeingResolved);
                var builder = ArrayBuilder<AliasAndForwardDirective>.GetInstance();

                foreach (var namespaceOrType in usings) {
                    if (!namespaceOrType.namespaceOrType.isNamespace)
                        continue;

                    if (namespaceOrType.namespaceOrType.IsFromCompilation(compilation))
                        continue;

                    var forwards = ((NamespaceSymbol)namespaceOrType.namespaceOrType)
                        .GetForwardAliases(basesBeingResolved);

                    foreach (var forward in forwards) {
                        if (forward.unresolvedTarget is not null) {
                            var parts = forward.unresolvedTarget.Split(';');
                            Debug.Assert(parts.Length == 2);
                            var aliasName = parts[0];
                            var name = SyntaxFactory.ParseName(parts[1]);
                            var tempDiagnostics = BelteDiagnosticQueue.GetInstance();

                            var symbol = BindNamespaceOrTypeOrMethodSymbol(
                                name,
                                tempDiagnostics,
                                out var templateArguments,
                                basesBeingResolved
                            );

                            if (forward.isUnresolvedType != symbol.IsTypeOrTypeAlias() || tempDiagnostics.AnyErrors()) {
                                tempDiagnostics.Free();
                                continue;
                            }

                            tempDiagnostics.Free();

                            var syntax = SyntaxFactory.ForwardDirective(
                                SyntaxFactory.Token(SyntaxKind.ForwardKeyword),
                                name,
                                SyntaxFactory.Token(SyntaxKind.SemicolonToken)
                            );

                            _lazyUnresolvedForwardsSyntax ??= [];
                            _lazyUnresolvedForwardsSyntax.TryAdd(forward, syntax);

                            var alias = new AliasSymbolFromResolvedTarget(
                                symbol,
                                aliasName,
                                namespaceOrType.namespaceOrType,
                                templateArguments,
                                [namespaceOrType.namespaceOrType.location],
                                isExtern: false
                            );

                            var newAliasAndForward = new AliasAndForwardDirective(alias, syntax);
                            builder.Add(newAliasAndForward);

                            _lazyForwardsNeedingMapping ??= [];
                            _lazyForwardsNeedingMapping.Add(newAliasAndForward);
                        }
                    }
                }

                ImmutableInterlocked.InterlockedInitialize(
                    ref _lazyForwardUnresolvedAliases,
                    builder.ToImmutableAndFree()
                );
            }
        }

        internal override ImmutableDictionary<string, AliasAndForwardDirective> GetForwardAliasesMap(
            ConsList<TypeSymbol> basesBeingResolved) {
            var usings = GetUsings(basesBeingResolved);
            var builder = ImmutableDictionary.CreateBuilder<string, AliasAndForwardDirective>();
            GetForwardAliasesUnresolvedOnly(basesBeingResolved);

            foreach (var namespaceOrType in usings) {
                if (!namespaceOrType.namespaceOrType.isNamespace)
                    continue;

                var forwards = ((NamespaceSymbol)namespaceOrType.namespaceOrType)
                    .GetForwardAliasesMap(basesBeingResolved);

                builder.AddRange(forwards);

                if (_lazyForwardsNeedingMapping is not null) {
                    foreach (var forward in _lazyForwardsNeedingMapping)
                        builder.Add(forward.alias.name, forward);
                }
            }

            return builder.ToImmutable();
        }

        internal override ImmutableDictionary<ForwardDirectiveSyntax, SyntaxReference> GetForwardAliasesOrigins(
            ConsList<TypeSymbol> basesBeingResolved) {
            var usings = GetUsings(basesBeingResolved);
            var builder = ImmutableDictionary.CreateBuilder<ForwardDirectiveSyntax, SyntaxReference>();

            foreach (var namespaceOrType in usings) {
                if (!namespaceOrType.namespaceOrType.isNamespace)
                    continue;

                var forwards = ((NamespaceSymbol)namespaceOrType.namespaceOrType)
                    .GetForwardAliases(basesBeingResolved);

                foreach (var forward in forwards) {
                    if (forward.unresolvedTarget is not null)
                        builder.Add(_lazyUnresolvedForwardsSyntax[forward], namespaceOrType.usingDirectiveReference);
                    else if (forward.forwardDirective is not null)
                        builder.Add(forward.forwardDirective, namespaceOrType.usingDirectiveReference);
                }
            }

            return builder.ToImmutable();
        }

        private protected override SyntaxReference GetForwardOrigin(ForwardDirectiveSyntax forwardDirective) {
            _ = GetForwards(null);
            return _lazyForwardOrigins[forwardDirective];
        }

        private protected override Imports GetImports() {
            return _declaringSymbol.GetImports(_declarationSyntax, basesBeingResolved: null);
        }
    }
}
