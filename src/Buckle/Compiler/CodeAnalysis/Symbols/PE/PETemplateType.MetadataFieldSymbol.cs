using System.Collections.Immutable;
using System.Reflection;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed partial class PETemplateType {
    internal sealed partial class MetadataFieldSymbol : FieldSymbol {
        private readonly string _name;
        private readonly FieldAttributes _flags;
        private readonly TemplateMetadataWriter.FieldFlags _additionalFlags;
        private readonly PETemplateType _containingType;
        private readonly TypeWithAnnotations _type;
        private readonly ImmutableArray<AttributeData> _attributes;
        private readonly ConstantValue _constantValue;

        private readonly FieldSymbol _fieldToLink;

        internal MetadataFieldSymbol(
            PETemplateType containingType,
            string name,
            FieldAttributes flags,
            TemplateMetadataWriter.FieldFlags additionalFlags,
            TypeSymbol type,
            AttributeData[] attributes,
            ConstantValue constantValue) {
            _attributes = attributes.ToImmutableArray();
            _containingType = containingType;
            _name = name;
            _flags = flags;
            _additionalFlags = additionalFlags;
            _type = new TypeWithAnnotations(type);
            _constantValue = constantValue;
        }

        internal MetadataFieldSymbol(
            PETemplateType containingType,
            string name,
            FieldAttributes flags,
            TemplateMetadataWriter.FieldFlags additionalFlags,
            TypeSymbol type,
            AttributeData[] attributes,
            ConstantValue constantValue,
            FieldSymbol fieldToLink)
            : this(containingType, name, flags, additionalFlags, type, attributes, constantValue) {
            _fieldToLink = fieldToLink;
        }

        public override string name => _name;

        public override RefKind refKind
            => (_additionalFlags & TemplateMetadataWriter.FieldFlags.ByRef) != 0 ? RefKind.Ref : RefKind.None;

        public override bool isConst => false;

        public override bool isFinal => (_flags & FieldAttributes.InitOnly) != 0;

        public override bool isConstExpr {
            get {
                return (_flags & FieldAttributes.Literal) != 0 ||
                    GetConstantValue(ConstantFieldsInProgress.Empty) is not null;
            }
        }

        internal override Symbol containingSymbol => _containingType;

        internal override NamedTypeSymbol containingType => _containingType;

        internal override FieldSymbol originalDefinition => _fieldToLink ?? base.originalDefinition;

        internal FieldAttributes flags => _flags;

        internal override ImmutableArray<TextLocation> locations
            => _containingType.containingPEModule.metadataLocation.Cast<MetadataLocation, TextLocation>();

        internal override ImmutableArray<SyntaxReference> declaringSyntaxReferences => [];

        internal override SyntaxReference syntaxReference => null;

        internal override TextLocation location => locations[0];

        internal override Accessibility declaredAccessibility {
            get {
                var access = (_flags & FieldAttributes.FieldAccessMask) switch {
                    FieldAttributes.Assembly => Accessibility.Internal,
                    FieldAttributes.FamORAssem => Accessibility.InternalOrProtected,
                    FieldAttributes.FamANDAssem => Accessibility.InternalAndProtected,
                    FieldAttributes.Private or FieldAttributes.PrivateScope => Accessibility.Private,
                    FieldAttributes.Public => Accessibility.Public,
                    FieldAttributes.Family => Accessibility.Protected,
                    _ => Accessibility.Private,
                };

                return access;
            }
        }

        internal override bool isStatic => (_flags & FieldAttributes.Static) != 0;

        internal sealed override Compilation declaringCompilation => null;

        internal override ImmutableArray<AttributeData> GetAttributes() {
            return _attributes;
        }

        internal override TypeWithAnnotations GetFieldType(ConsList<FieldSymbol> fieldsBeingBound) {
            return _type;
        }

        internal override ConstantValue GetConstantValue(ConstantFieldsInProgress inProgress) {
            return _constantValue;
        }
    }
}
