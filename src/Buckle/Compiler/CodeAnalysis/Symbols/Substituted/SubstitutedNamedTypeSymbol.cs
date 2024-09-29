using System.Collections.Immutable;
using System.Threading;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class SubstitutedNamedTypeSymbol : WrappedNamedTypeSymbol {
    private readonly TemplateMap _inputMap;

    private TemplateMap _lazyMap;
    private ImmutableArray<TemplateParameterSymbol> _lazyTypeParameters;

    private protected SubstitutedNamedTypeSymbol(
        Symbol newContainer,
        TemplateMap templateMap,
        NamedTypeSymbol originalDefinition,
        NamedTypeSymbol constructedFrom = null,
        bool isUnboundTemplateType = false) {
        containingSymbol = newContainer;
        _inputMap = templateMap;
        this.isUnboundTemplateType = isUnboundTemplateType;
    }

    public sealed override SymbolKind kind => originalDefinition.kind;

    public sealed override TemplateMap templateSubstitution {
        get {
            EnsureMapAndTypeParameters();
            return _lazyMap;
        }
    }

    internal sealed override bool isUnboundTemplateType { get; }

    internal sealed override Symbol containingSymbol { get; }

    internal sealed override NamedTypeSymbol originalDefinition => underlyingNamedType;

    internal override NamedTypeSymbol containingType => containingSymbol as NamedTypeSymbol;

    internal sealed override NamedTypeSymbol GetDeclaredBaseType(ConsList<TypeSymbol> basesBeingResolved) {
        return isUnboundTemplateType
            ? null
            : templateSubstitution.SubstituteNamedType(originalDefinition.GetDeclaredBaseType(basesBeingResolved));
    }

    private void EnsureMapAndTypeParameters() {
        if (!_lazyTypeParameters.IsDefault)
            return;

        var newMap = _inputMap.WithAlphaRename(originalDefinition, this, out var typeParameters);
        var previousMap = Interlocked.CompareExchange(ref _lazyMap, newMap, null);

        if (previousMap is not null)
            typeParameters = previousMap.SubstituteTypeParameters(originalDefinition.templateParameters);

        ImmutableInterlocked.InterlockedCompareExchange(
            ref _lazyTypeParameters,
            typeParameters,
            default(ImmutableArray<TemplateParameterSymbol>)
        );
    }
}
