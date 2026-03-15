using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class PEGlobalNamespaceSymbol : PENamespaceSymbol {
    private readonly PEModuleSymbol _moduleSymbol;

    internal PEGlobalNamespaceSymbol(PEModuleSymbol moduleSymbol) {
        _moduleSymbol = moduleSymbol;
    }

    public override string name => "";

    public override bool isGlobalNamespace => true;

    internal override Symbol containingSymbol => _moduleSymbol;

    internal override PEModuleSymbol containingPEModule => _moduleSymbol;

    internal override AssemblySymbol containingAssembly => _moduleSymbol.containingAssembly;

    internal sealed override Compilation declaringCompilation => null;

    private protected override void EnsureAllMembersLoaded() {
        if (_lazyTypes is null || _lazyNamespaces is null) {
            IEnumerable<IGrouping<string, TypeDefinitionHandle>> groups;

            try {
                groups = _moduleSymbol.module.GroupTypesByNamespaceOrThrow(StringComparer.Ordinal);
            } catch (BadImageFormatException) {
                groups = SpecializedCollections.EmptyEnumerable<IGrouping<string, TypeDefinitionHandle>>();
            }

            LoadAllMembers(groups);
        }
    }
}
