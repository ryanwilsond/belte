using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Threading;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class PENestedNamespaceSymbol : PENamespaceSymbol {
    private readonly PENamespaceSymbol _containingNamespaceSymbol;
    private readonly string _name;
    private IEnumerable<IGrouping<string, TypeDefinitionHandle>> _typesByNS;

    internal PENestedNamespaceSymbol(
        string name,
        PENamespaceSymbol containingNamespace,
        IEnumerable<IGrouping<string, TypeDefinitionHandle>> typesByNS) {
        _containingNamespaceSymbol = containingNamespace;
        _name = name;
        _typesByNS = typesByNS;
    }

    public override string name => _name;

    public override bool isGlobalNamespace => false;

    internal override Symbol containingSymbol => _containingNamespaceSymbol;

    internal override PEModuleSymbol containingPEModule => _containingNamespaceSymbol.containingPEModule;

    internal override AssemblySymbol containingAssembly => containingPEModule.containingAssembly;

    private protected override void EnsureAllMembersLoaded() {
        var typesByNS = _typesByNS;

        if (_lazyTypes is null || _lazyNamespaces is null) {
            System.Diagnostics.Debug.Assert(typesByNS is not null);
            LoadAllMembers(typesByNS);
            Interlocked.Exchange(ref _typesByNS, null);
        }
    }

    internal sealed override Compilation declaringCompilation => null;
}
