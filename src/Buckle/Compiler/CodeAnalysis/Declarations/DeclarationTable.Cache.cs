using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Buckle.CodeAnalysis;

internal partial class DeclarationTable {
    private class Cache {
        private readonly DeclarationTable _table;
        private MergedNamespaceDeclaration _mergedRoot;

        private ISet<string> _typeNames;
        private ISet<string> _namespaceNames;

        internal Cache(DeclarationTable table) {
            _table = table;
        }

        internal MergedNamespaceDeclaration mergedRoot {
            get {
                if (_mergedRoot is null) {
                    Interlocked.CompareExchange(
                        ref _mergedRoot,
                        MergedNamespaceDeclaration.Create(_table._allOlderRootDeclarations.InInsertionOrder
                            .Select(static lazyRoot => lazyRoot.Value).AsImmutable<SingleNamespaceDeclaration>()),
                        comparand: null
                    );
                }

                return _mergedRoot;
            }
        }

        internal ISet<string> typeNames {
            get {
                if (_typeNames is null)
                    Interlocked.CompareExchange(ref _typeNames, GetTypeNames(mergedRoot), comparand: null);

                return _typeNames;
            }
        }

        internal ISet<string> namespaceNames {
            get {
                if (_namespaceNames is null)
                    Interlocked.CompareExchange(ref _namespaceNames, GetNamespaceNames(mergedRoot), comparand: null);

                return _namespaceNames;
            }
        }
    }
}
