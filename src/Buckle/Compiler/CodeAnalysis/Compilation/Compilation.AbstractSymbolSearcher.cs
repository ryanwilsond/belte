using System.Collections.Generic;
using System.Linq;
using Buckle.CodeAnalysis.Symbols;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis;

public sealed partial class Compilation {
    private abstract class AbstractSymbolSearcher {
        private readonly PooledDictionary<Declaration, NamespaceOrTypeSymbol> _cache;
        private readonly Compilation _compilation;
        private readonly bool _includeNamespace;
        private readonly bool _includeType;
        private readonly bool _includeMember;

        private protected AbstractSymbolSearcher(Compilation compilation, SymbolFilter filter) {
            _cache = PooledDictionary<Declaration, NamespaceOrTypeSymbol>.GetInstance();

            _compilation = compilation;

            _includeNamespace = (filter & SymbolFilter.Namespace) == SymbolFilter.Namespace;
            _includeType = (filter & SymbolFilter.Type) == SymbolFilter.Type;
            _includeMember = (filter & SymbolFilter.Member) == SymbolFilter.Member;
        }

        private protected abstract bool Matches(string name);
        private protected abstract bool ShouldCheckTypeForMembers(MergedTypeDeclaration current);

        internal IEnumerable<Symbol> GetSymbolsWithName() {
            var result = new HashSet<Symbol>();
            var spine = ArrayBuilder<MergedNamespaceOrTypeDeclaration>.GetInstance();

            AppendSymbolsWithName(spine, _compilation.mergedRootDeclaration, result);

            spine.Free();
            _cache.Free();
            return result;
        }

        private void AppendSymbolsWithName(
            ArrayBuilder<MergedNamespaceOrTypeDeclaration> spine,
            MergedNamespaceOrTypeDeclaration current,
            HashSet<Symbol> set) {
            if (current.kind == DeclarationKind.Namespace) {
                if (_includeNamespace && Matches(current.name)) {
                    var container = GetSpineSymbol(spine);
                    var symbol = GetSymbol(container, current);

                    if (symbol is not null)
                        set.Add(symbol);
                }
            } else {
                if (_includeType && Matches(current.name)) {
                    var container = GetSpineSymbol(spine);
                    var symbol = GetSymbol(container, current);

                    if (symbol is not null)
                        set.Add(symbol);
                }

                if (_includeMember) {
                    var typeDeclaration = (MergedTypeDeclaration)current;
                    if (ShouldCheckTypeForMembers(typeDeclaration)) {
                        AppendMemberSymbolsWithName(spine, typeDeclaration, set);
                    }
                }
            }

            spine.Add(current);

            foreach (var child in current.children) {
                if (child is MergedNamespaceOrTypeDeclaration mergedNamespaceOrType) {
                    if (_includeMember || _includeType || child.kind == DeclarationKind.Namespace)
                        AppendSymbolsWithName(spine, mergedNamespaceOrType, set);
                }
            }

            spine.RemoveAt(spine.Count - 1);
        }

        private void AppendMemberSymbolsWithName(
            ArrayBuilder<MergedNamespaceOrTypeDeclaration> spine,
            MergedTypeDeclaration current,
            HashSet<Symbol> set) {
            spine.Add(current);

            var container = GetSpineSymbol(spine);

            if (container is not null) {
                foreach (var member in container.GetMembers()) {
                    if (!member.IsTypeOrTypeAlias() && member.canBeReferencedByName && Matches(member.name))
                        set.Add(member);
                }
            }

            spine.RemoveAt(spine.Count - 1);
        }

        private protected NamespaceOrTypeSymbol GetSpineSymbol(ArrayBuilder<MergedNamespaceOrTypeDeclaration> spine) {
            if (spine.Count == 0)
                return null;

            var symbol = GetCachedSymbol(spine[spine.Count - 1]);

            if (symbol is not null)
                return symbol;

            NamespaceOrTypeSymbol current = _compilation.globalNamespaceInternal;

            for (var i = 1; i < spine.Count; i++)
                current = GetSymbol(current, spine[i]);

            return current;
        }

        private NamespaceOrTypeSymbol GetCachedSymbol(MergedNamespaceOrTypeDeclaration declaration) {
            return _cache.TryGetValue(declaration, out var symbol) ? symbol : null;
        }

        private NamespaceOrTypeSymbol GetSymbol(NamespaceOrTypeSymbol container, MergedNamespaceOrTypeDeclaration declaration) {
            if (container is null)
                return _compilation.globalNamespaceInternal;

            if (declaration.kind == DeclarationKind.Namespace)
                AddCache(container.GetMembers(declaration.name).OfType<NamespaceOrTypeSymbol>());
            else
                AddCache(container.GetTypeMembers(declaration.name));

            return GetCachedSymbol(declaration);
        }

        private void AddCache(IEnumerable<NamespaceOrTypeSymbol> symbols) {
            foreach (var symbol in symbols) {
                var mergedNamespace = symbol as MergedNamespaceSymbol;

                if (mergedNamespace is not null) {
                    _cache[mergedNamespace.constituentNamespaces.OfType<SourceNamespaceSymbol>().First().mergedDeclaration] = symbol;
                    continue;
                }

                var sourceNamespace = symbol as SourceNamespaceSymbol;

                if (sourceNamespace is not null) {
                    _cache[sourceNamespace.mergedDeclaration] = sourceNamespace;
                    continue;
                }

                if (symbol is SourceMemberContainerTypeSymbol sourceType)
                    _cache[sourceType.mergedDeclaration] = sourceType;
            }
        }
    }
}
