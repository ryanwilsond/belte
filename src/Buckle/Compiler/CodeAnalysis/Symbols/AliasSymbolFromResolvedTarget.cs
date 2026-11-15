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
        templateArguments = [];
    }

    internal AliasSymbolFromResolvedTarget(
        Symbol target,
        string aliasName,
        Symbol containingSymbol,
        ImmutableArray<TypeOrConstant> templateArguments,
        ImmutableArray<TextLocation> locations,
        bool isExtern)
        : base(aliasName, containingSymbol, locations, isExtern) {
        _aliasTarget = target;
        this.templateArguments = templateArguments;
    }

    public override Symbol target => _aliasTarget;

    internal override bool requiresCompletion => false;

    internal override ImmutableArray<TypeOrConstant> templateArguments { get; }

    internal override Symbol GetAliasTarget(ConsList<TypeSymbol> basesBeingResolved) {
        return _aliasTarget;
    }
}
