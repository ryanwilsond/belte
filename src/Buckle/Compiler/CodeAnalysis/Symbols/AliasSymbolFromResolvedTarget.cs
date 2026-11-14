using System.Collections.Immutable;
using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class AliasSymbolFromResolvedTarget : AliasSymbol {
    private readonly Symbol _aliasTarget;

    internal AliasSymbolFromResolvedTarget(
        Symbol target,
        string aliasName,
        Symbol containingSymbol,
        ImmutableArray<TextLocation> locations,
        bool isExtern)
        : base(aliasName, containingSymbol, locations, isExtern) {
        _aliasTarget = target;
    }

    public override Symbol target => _aliasTarget;

    internal override bool requiresCompletion => false;

    internal override Symbol GetAliasTarget(ConsList<TypeSymbol> basesBeingResolved) {
        return _aliasTarget;
    }
}
