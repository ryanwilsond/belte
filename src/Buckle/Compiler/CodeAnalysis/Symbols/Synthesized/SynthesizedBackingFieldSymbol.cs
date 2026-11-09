using System.Collections.Immutable;
using System.Diagnostics;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class SynthesizedBackingFieldSymbol : SynthesizedBackingFieldSymbolBase {
    private readonly SourcePropertySymbolBase _property;

    internal SynthesizedBackingFieldSymbol(
        SourcePropertySymbolBase property,
        string name,
        bool isConst,
        bool isStatic,
        bool hasInitializer)
        : base(name, isConst, isStatic) {
        Debug.Assert(!string.IsNullOrEmpty(name));
        Debug.Assert(property.refKind is RefKind.None or RefKind.Ref or RefKind.RefConst);
        _property = property;
        this.hasInitializer = hasInitializer;
    }

    internal override bool hasInitializer { get; }

    private protected override IAttributeTargetSymbol _attributeOwner => _property.attributesOwner;

    internal override TextLocation errorLocation => _property.location;

    public override Symbol associatedSymbol => _property;

    internal override ImmutableArray<TextLocation> locations => _property.locations;

    internal override TextLocation location => _property.location;

    public override RefKind refKind => _property.refKind;

    internal override Symbol containingSymbol => _property.containingSymbol;

    internal override NamedTypeSymbol containingType => _property.containingType;

    internal override TypeWithAnnotations GetFieldType(ConsList<FieldSymbol> fieldsBeingBound)
        => _property.typeWithAnnotations;

    private protected override OneOrMany<SyntaxList<AttributeListSyntax>> GetAttributeDeclarations() {
        var property = _property;
        return property.GetAttributeDeclarations();
    }

    private protected sealed override void DecodeWellKnownAttributeImpl(
        ref DecodeWellKnownAttributeArguments<AttributeSyntax, AttributeData, AttributeLocation> arguments) {
        Debug.Assert(arguments.attributeSyntax != null);

        var attribute = arguments.attribute;
        Debug.Assert(!attribute.hasErrors);
        Debug.Assert(arguments.symbolPart == AttributeLocation.None);

        if (attribute.IsTargetAttribute(AttributeDescription.FixedBufferAttribute)) {
            // TODO PE
            throw ExceptionUtilities.Unreachable();
            // ((BindingDiagnosticBag)arguments.Diagnostics).Add(ErrorCode.ERR_DoNotUseFixedBufferAttrOnProperty, arguments.AttributeSyntaxOpt.Name.Location);
        } else {
            base.DecodeWellKnownAttributeImpl(ref arguments);
        }
    }
}
