using System.Collections.Immutable;
using System.Diagnostics;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class SourceClonedParameterSymbol : SourceParameterSymbolBase {
    private readonly bool _suppressOptional;

    protected readonly SourceParameterSymbol _originalParam;

    internal SourceClonedParameterSymbol(
        SourceParameterSymbol originalParam,
        Symbol newOwner,
        int newOrdinal,
        bool suppressOptional)
        : base(newOwner, newOrdinal) {
        Debug.Assert(originalParam is not null);
        _suppressOptional = suppressOptional;
        _originalParam = originalParam;
    }

    public sealed override string name => _originalParam.name;

    internal override bool isImplicitlyDeclared => true;

    internal override ImmutableArray<SyntaxReference> declaringSyntaxReferences => [];

    internal override SyntaxReference syntaxReference => null;

    internal override bool isMetadataOptional =>
        // _suppressOptional ? _originalParam.hasOptionalAttribute : _originalParam.isMetadataOptional;
        _originalParam.isMetadataOptional;

    internal sealed override ScopedKind effectiveScope => _originalParam.effectiveScope;

    internal override bool hasUnscopedRefAttribute => _originalParam.hasUnscopedRefAttribute;

    internal override ConstantValue outDefaultValue => _originalParam.outDefaultValue;

    internal override bool isConst => _originalParam.isConst;

    internal override ConstantValue explicitDefaultConstantValue
        // => _suppressOptional
        //     ? _originalParam.defaultValueFromAttributes
        //     : _originalParam.explicitDefaultConstantValue;
        => _originalParam.explicitDefaultConstantValue;

    internal override TypeWithAnnotations typeWithAnnotations => _originalParam.typeWithAnnotations;

    public override RefKind refKind => _originalParam.refKind;

    internal override bool isMetadataOut => _originalParam.isMetadataOut;

    internal override ImmutableArray<TextLocation> locations => _originalParam.locations;

    internal override TextLocation location => _originalParam.location;

    internal override ImmutableArray<AttributeData> GetAttributes() {
        return _originalParam.GetAttributes();
    }
}
