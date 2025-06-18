using System.Collections.Generic;
using System.Threading;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class MetadataOrSourceAssemblySymbol : NonMissingAssemblySymbol {
    private ICollection<string> _lazyTypeNames;
    private ICollection<string> _lazyNamespaceNames;

    internal override ICollection<string> typeNames {
        get {
            if (_lazyTypeNames is null)
                Interlocked.CompareExchange(ref _lazyTypeNames, declaringCompilation.declarationTable.typeNames, null);

            return _lazyTypeNames;
        }
    }

    internal override ICollection<string> namespaceNames {
        get {
            if (_lazyNamespaceNames == null) {
                Interlocked.CompareExchange(
                    ref _lazyNamespaceNames,
                    declaringCompilation.declarationTable.namespaceNames,
                    null
                );
            }

            return _lazyNamespaceNames;
        }
    }
}
