
namespace Buckle.CodeAnalysis.Symbols;

internal sealed class SynthesizedComplexParameterSymbol : SynthesizedParameterSymbolBase {
    private readonly SourceComplexParameterSymbolBase _baseParameter;
    private readonly ConstantValue _defaultValue;

    internal SynthesizedComplexParameterSymbol(
        Symbol container,
        TypeWithAnnotations type,
        int ordinal,
        RefKind refKind,
        ScopedKind scope,
        ConstantValue defaultValue,
        string name,
        SourceComplexParameterSymbolBase baseParameter)
        : base(container, type, ordinal, refKind, scope, name) {
        _defaultValue = defaultValue;
        _baseParameter = baseParameter;
    }

    internal override bool isMetadataOptional => _baseParameter?.isMetadataOptional ?? base.isMetadataOptional;

    internal override ConstantValue explicitDefaultConstantValue
        => _baseParameter?.explicitDefaultConstantValue ?? _defaultValue;
}
