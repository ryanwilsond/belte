using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;
using TemplateTypeDecoder = Buckle.CodeAnalysis.TemplateMetadataReader.TemplateMetadata.TemplateTypeDecoder;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed partial class PETemplateType {
    internal sealed class MetadataPropertySymbol : PropertySymbol {
        private readonly TemplateTypeDecoder _parentDecoder;
        private readonly string _name;
        private readonly PropertyAttributes _flags;
        private readonly TemplateMetadataWriter.PropertyFlags _additionalFlags;
        private readonly PETemplateType _containingType;
        private readonly TypeWithAnnotations _type;
        private readonly uint _getMethodIndex;
        private readonly uint _setMethodIndex;
        private readonly ImmutableArray<AttributeData> _attributes;

        private MethodSymbol _lazyGetMethod;
        private MethodSymbol _lazySetMethod;
        private ImmutableArray<ParameterSymbol> _lazyParameters;

        private readonly PropertySymbol _propertyToLink;

        internal MetadataPropertySymbol(
            TemplateTypeDecoder parentDecoder,
            PETemplateType containingType,
            string name,
            PropertyAttributes flags,
            TemplateMetadataWriter.PropertyFlags additionalFlags,
            TypeSymbol type,
            uint getMethodIndex,
            uint setMethodIndex,
            AttributeData[] attributes) {
            _parentDecoder = parentDecoder;
            _attributes = attributes.ToImmutableArray();
            _containingType = containingType;
            _name = name;
            _flags = flags;
            _additionalFlags = additionalFlags;
            _getMethodIndex = getMethodIndex;
            _setMethodIndex = setMethodIndex;
            _type = new TypeWithAnnotations(type);
        }

        internal MetadataPropertySymbol(
            TemplateTypeDecoder parentDecoder,
            PETemplateType containingType,
            string name,
            PropertyAttributes flags,
            TemplateMetadataWriter.PropertyFlags additionalFlags,
            TypeSymbol type,
            uint getMethodIndex,
            uint setMethodIndex,
            AttributeData[] attributes,
            PropertySymbol propertyToLink)
            : this(
                parentDecoder,
                containingType,
                name,
                flags,
                additionalFlags,
                type,
                getMethodIndex,
                setMethodIndex,
                attributes) {
            _propertyToLink = propertyToLink;
        }

        public override string name => _name;

        internal override RefKind refKind => getMethod.refKind;

        internal override Symbol containingSymbol => _containingType;

        internal override NamedTypeSymbol containingType => _containingType;

        public override PropertySymbol originalDefinition => _propertyToLink ?? base.originalDefinition;

        internal PropertyAttributes flags => _flags;

        internal override ImmutableArray<TextLocation> locations
            => _containingType.containingPEModule.metadataLocation.Cast<MetadataLocation, TextLocation>();

        internal override ImmutableArray<SyntaxReference> declaringSyntaxReferences => [];

        internal override SyntaxReference syntaxReference => null;

        internal override TextLocation location => locations[0];

        internal override TypeWithAnnotations typeWithAnnotations => _type;

        internal override bool mustCallMethodsDirectly => false;

        internal override bool hasSpecialName => (_flags & PropertyAttributes.SpecialName) != 0;

        internal override Accessibility declaredAccessibility
            => PEPropertyOrEventHelpers.GetDeclaredAccessibilityFromAccessors(getMethod, setMethod);

        internal override bool isStatic
            => (getMethod is null || getMethod.isStatic) && (setMethod is null || setMethod.isStatic);

        internal override CallingConvention callingConvention => CallingConvention.Default;

        internal override bool isExtern
            => (getMethod is not null && getMethod.isExtern) || (setMethod is not null && setMethod.isExtern);

        internal override bool isAbstract
            => (getMethod is not null && getMethod.isAbstract) || (setMethod is not null && setMethod.isAbstract);

        internal override bool isSealed
            => (getMethod is null || getMethod.isSealed) && (setMethod is null || setMethod.isSealed);

        internal override bool isVirtual {
            get {
                return !isOverride && !isAbstract &&
                    ((getMethod is not null && getMethod.isVirtual) || (setMethod is not null && setMethod.isVirtual));
            }
        }

        internal override bool isOverride
            => (getMethod is not null && getMethod.isOverride) || (setMethod is not null && setMethod.isOverride);

        internal sealed override Compilation declaringCompilation => null;

        internal override ImmutableArray<PropertySymbol> explicitInterfaceImplementations {
            get {
                if ((getMethod is null || getMethod.explicitInterfaceImplementations.Length == 0) &&
                    (setMethod is null || setMethod.explicitInterfaceImplementations.Length == 0)) {
                    return [];
                }

                var propertiesWithImplementedGetters =
                    PEPropertyOrEventHelpers.GetPropertiesForExplicitlyImplementedAccessor(getMethod);
                var propertiesWithImplementedSetters =
                    PEPropertyOrEventHelpers.GetPropertiesForExplicitlyImplementedAccessor(setMethod);

                var builder = ArrayBuilder<PropertySymbol>.GetInstance();

                foreach (var prop in propertiesWithImplementedGetters) {
                    if (!prop.setMethod.IsImplementable() || propertiesWithImplementedSetters.Contains(prop))
                        builder.Add(prop);
                }

                foreach (var prop in propertiesWithImplementedSetters) {
                    if (!prop.getMethod.IsImplementable())
                        builder.Add(prop);
                }

                return builder.ToImmutableAndFree();
            }
        }

        internal override MethodSymbol getMethod {
            get {
                if ((_additionalFlags & TemplateMetadataWriter.PropertyFlags.HasGetter) == 0)
                    return null;

                if (_lazyGetMethod is null) {
                    var getMethod = _parentDecoder.ResolveMethod(_getMethodIndex) as MetadataMethodSymbol;
                    getMethod.SetAssociatedProperty(this, MethodKind.PropertyGet);
                    Interlocked.CompareExchange(ref _lazyGetMethod, getMethod, null);
                    Debug.Assert(_lazyGetMethod is not null);
                }

                return _lazyGetMethod;
            }
        }

        internal override MethodSymbol setMethod {
            get {
                if ((_additionalFlags & TemplateMetadataWriter.PropertyFlags.HasSetter) == 0)
                    return null;

                if (_lazySetMethod is null) {
                    var setMethod = _parentDecoder.ResolveMethod(_setMethodIndex) as MetadataMethodSymbol;
                    setMethod.SetAssociatedProperty(this, MethodKind.PropertySet);
                    Interlocked.CompareExchange(ref _lazySetMethod, setMethod, null);
                    Debug.Assert(_lazySetMethod is not null);
                }

                return _lazySetMethod;
            }
        }

        internal override ImmutableArray<ParameterSymbol> parameters {
            get {
                EnsureParametersAreLoaded();
                return _lazyParameters;
            }
        }

        internal override ImmutableArray<AttributeData> GetAttributes() {
            return _attributes;
        }

        private ImmutableArray<ParameterSymbol> EnsureParametersAreLoaded() {
            var parameters = _lazyParameters;

            if (!parameters.IsDefault)
                return parameters;

            return InterlockedOperations.Initialize(ref _lazyParameters, LoadParameters());
        }

        private ImmutableArray<ParameterSymbol> LoadParameters() {
            // TODO
            return [];
        }
    }
}
