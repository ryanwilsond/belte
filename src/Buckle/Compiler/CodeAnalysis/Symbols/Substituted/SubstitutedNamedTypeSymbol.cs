using System.Collections.Immutable;
using System.Threading;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class SubstitutedNamedTypeSymbol : WrappedNamedTypeSymbol {
    private readonly TemplateMap _inputMap;

    private TemplateMap _lazyMap;
    private ImmutableArray<TemplateParameterSymbol> _lazyTemplateParameters;
    private NamedTypeSymbol _lazyBaseType = ErrorTypeSymbol.UnknownResultType;

    private protected SubstitutedNamedTypeSymbol(
        Symbol newContainer,
        TemplateMap templateMap,
        NamedTypeSymbol originalDefinition,
        NamedTypeSymbol constructedFrom = null,
        bool isUnboundTemplateType = false) : base(originalDefinition) {
        containingSymbol = newContainer;
        _inputMap = templateMap;
        this.isUnboundTemplateType = isUnboundTemplateType;

        if (constructedFrom is not null) {
            _lazyTemplateParameters = constructedFrom.templateParameters;
            _lazyMap = templateMap;
        }
    }

    public sealed override SymbolKind kind => originalDefinition.kind;

    public sealed override TemplateMap templateSubstitution {
        get {
            EnsureMapAndTemplateParameters();
            return _lazyMap;
        }
    }

    public sealed override ImmutableArray<TemplateParameterSymbol> templateParameters {
        get {
            EnsureMapAndTemplateParameters();
            return _lazyTemplateParameters;
        }
    }

    internal sealed override bool isUnboundTemplateType { get; }

    internal sealed override Symbol containingSymbol { get; }

    internal sealed override NamedTypeSymbol originalDefinition => underlyingNamedType;

    internal override NamedTypeSymbol containingType => containingSymbol as NamedTypeSymbol;

    internal sealed override NamedTypeSymbol baseType {
        get {
            if (isUnboundTemplateType)
                return null;

            if (ReferenceEquals(_lazyBaseType, ))
        }
    }

    internal sealed override NamedTypeSymbol GetDeclaredBaseType(ConsList<TypeSymbol> basesBeingResolved) {
        return isUnboundTemplateType
            ? null
            : templateSubstitution.SubstituteNamedType(originalDefinition.GetDeclaredBaseType(basesBeingResolved));
    }

    private void EnsureMapAndTemplateParameters() {
        if (!_lazyTemplateParameters.IsDefault)
            return;

        var newMap = _inputMap.WithAlphaRename(originalDefinition, this, out var typeParameters);
        var previousMap = Interlocked.CompareExchange(ref _lazyMap, newMap, null);

        if (previousMap is not null)
            typeParameters = previousMap.SubstituteTemplateParameters(originalDefinition.templateParameters);

        ImmutableInterlocked.InterlockedCompareExchange(
            ref _lazyTemplateParameters,
            typeParameters,
            default
        );
    }
}
