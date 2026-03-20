using System.Collections.Immutable;
using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class AliasSymbolFromResolvedTarget : AliasSymbol {
    private readonly NamespaceOrTypeSymbol _aliasTarget;

    internal AliasSymbolFromResolvedTarget(
        NamespaceOrTypeSymbol target,
        string aliasName,
        Symbol containingSymbol,
        ImmutableArray<TextLocation> locations)
        : base(aliasName, containingSymbol, locations) {
        _aliasTarget = target;
    }

    public override NamespaceOrTypeSymbol target => _aliasTarget;

    internal override bool requiresCompletion => false;

    internal override NamespaceOrTypeSymbol GetAliasTarget(ConsList<TypeSymbol>? basesBeingResolved) {
        return _aliasTarget;
    }
}
