using System.Collections.Immutable;
using System.Reflection;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using TemplateMethodDecoder = Buckle.CodeAnalysis.TemplateMetadataReader.TemplateMetadata.TemplateMethodDecoder;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed partial class PETemplateType {
    internal sealed class MetadataParameterSymbol : ParameterSymbol {
        private readonly Symbol _containingSymbol;
        private readonly string _name;
        private readonly TypeWithAnnotations _typeWithAnnotations;
        private readonly ParameterAttributes _flags;
        private readonly ushort _ordinal;
        private readonly TemplateMetadataWriter.ParameterFlags _additionalFlags;
        private readonly ConstantValue _defaultValue;
        private readonly ImmutableArray<AttributeData> _attributes;

        private readonly ParameterSymbol _symbolToLink;

        internal MetadataParameterSymbol(
            TemplateMethodDecoder decoder,
            MetadataMethodSymbol containingSymbol,
            ushort ordinal) {
            _containingSymbol = containingSymbol;
            _ordinal = ordinal;

            var info = decoder.DecodeParameter(ordinal);

            _name = info.name;
            _flags = info.attributes;
            _additionalFlags = info.flags;
            _typeWithAnnotations = new TypeWithAnnotations(info.type);
            _defaultValue = info.defaultValue;
            _attributes = info.customAttributes.ToImmutableArray();
        }

        internal MetadataParameterSymbol(
            TemplateMethodDecoder decoder,
            MetadataMethodSymbol containingSymbol,
            ushort ordinal,
            ParameterSymbol symbolToLink)
            : this(decoder, containingSymbol, ordinal) {
            _symbolToLink = symbolToLink;
        }

        public override RefKind refKind
            => (_additionalFlags & TemplateMetadataWriter.ParameterFlags.ByRef) != 0 ? RefKind.Ref : RefKind.None;

        public override string name => _name;

        public override int ordinal => _ordinal;

        internal override Symbol containingSymbol => _containingSymbol;

        internal bool hasMetadataConstantValue => (_flags & ParameterAttributes.HasDefault) != 0;

        internal override ImmutableArray<TextLocation> locations => _containingSymbol.locations;

        internal override ImmutableArray<SyntaxReference> declaringSyntaxReferences => [];

        internal override ParameterSymbol originalDefinition => _symbolToLink ?? base.originalDefinition;

        internal override SyntaxReference syntaxReference => null;

        internal override TextLocation location => locations[0];

        internal sealed override ScopedKind effectiveScope => ScopedKind.None;

        internal override bool hasUnscopedRefAttribute => false;

        internal override bool isMetadataOptional => (_flags & ParameterAttributes.Optional) != 0;

        internal override bool isMetadataOut => (_flags & ParameterAttributes.Out) != 0;

        internal override bool isConst => (_additionalFlags & TemplateMetadataWriter.ParameterFlags.IsConst) != 0;

        internal override ConstantValue outDefaultValue
            => (_additionalFlags & TemplateMetadataWriter.ParameterFlags.HasOutDefaultValue) != 0
                ? _defaultValue
                : null;

        internal override TypeWithAnnotations typeWithAnnotations => _typeWithAnnotations;

        internal override ConstantValue explicitDefaultConstantValue
            => (_flags & ParameterAttributes.HasDefault) != 0 ? _defaultValue : null;

        internal override ImmutableArray<AttributeData> GetAttributes() {
            return _attributes;
        }
    }
}
