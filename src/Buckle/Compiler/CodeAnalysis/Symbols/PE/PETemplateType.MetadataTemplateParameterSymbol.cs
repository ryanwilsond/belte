using System.Collections.Immutable;
using System.Reflection;
using System.Threading;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
using TemplateTypeDecoder = Buckle.CodeAnalysis.TemplateMetadataReader.TemplateMetadata.TemplateTypeDecoder;
using TemplateMethodDecoder = Buckle.CodeAnalysis.TemplateMetadataReader.TemplateMetadata.TemplateMethodDecoder;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed partial class PETemplateType {
    internal sealed class MetadataTemplateParameterSymbol : TemplateParameterSymbol {
        private readonly Symbol _containingSymbol;
        private readonly string _name;
        private readonly ushort _ordinal;
        private readonly GenericParameterAttributes _flags;
        private readonly TemplateMetadataWriter.TemplateParameterFlags _additionalFlags;
        private readonly TypeWithAnnotations _underlyingType;
        private readonly TypeOrConstant _defaultValue;

        private TypeParameterBounds _lazyBounds = TypeParameterBounds.Unset;
        // private ImmutableArray<TypeWithAnnotations> _lazyDeclaredConstraintTypes;

        private readonly TemplateParameterSymbol _symbolToLink;

        internal MetadataTemplateParameterSymbol(
            TemplateTypeDecoder decoder,
            PETemplateType definingNamedType,
            ushort ordinal) {
            _containingSymbol = definingNamedType;
            _ordinal = ordinal;
            containingModule = definingNamedType.containingPEModule;
            (_name, _flags, _additionalFlags, var underlyingType, _defaultValue)
                = decoder.DecodeTemplateParameter(ordinal);
            _underlyingType = new TypeWithAnnotations(underlyingType);
        }

        internal MetadataTemplateParameterSymbol(
            TemplateTypeDecoder decoder,
            PETemplateType definingNamedType,
            ushort ordinal,
            TemplateParameterSymbol symbolToLink)
            : this(decoder, definingNamedType, ordinal) {
            _symbolToLink = symbolToLink;
        }

        internal MetadataTemplateParameterSymbol(
            TemplateMethodDecoder decoder,
            MetadataMethodSymbol definingMethod,
            ushort ordinal) {
            _containingSymbol = definingMethod;
            _ordinal = ordinal;
            containingModule = ((PETemplateType)definingMethod.containingType).containingPEModule;
            (_name, _flags, _additionalFlags, var underlyingType, _defaultValue)
                = decoder.DecodeTemplateParameter(ordinal);
            _underlyingType = new TypeWithAnnotations(underlyingType);
        }

        internal MetadataTemplateParameterSymbol(
            TemplateMethodDecoder decoder,
            MetadataMethodSymbol definingMethod,
            ushort ordinal,
            TemplateParameterSymbol symbolToLink)
            : this(decoder, definingMethod, ordinal) {
            _symbolToLink = symbolToLink;
        }

        public override string name => _name;

        internal override TemplateParameterKind templateParameterKind
            => containingSymbol.kind == SymbolKind.Method
                ? TemplateParameterKind.Method
                : TemplateParameterKind.Type;

        internal override int ordinal => _ordinal;

        internal override PEModuleSymbol containingModule { get; }

        internal override Symbol containingSymbol => _containingSymbol;

        internal override TemplateParameterSymbol originalDefinition => _symbolToLink ?? base.originalDefinition;

        internal override AssemblySymbol containingAssembly => _containingSymbol.containingAssembly;

        internal override ImmutableArray<TextLocation> locations => _containingSymbol.locations;

        internal override ImmutableArray<SyntaxReference> declaringSyntaxReferences => [];

        internal sealed override Compilation declaringCompilation => null;

        internal override bool isOptional
            => (_additionalFlags & TemplateMetadataWriter.TemplateParameterFlags.HasDefaultValue) != 0;

        internal override bool isCompileTimeType
            => (_additionalFlags & TemplateMetadataWriter.TemplateParameterFlags.CompileTime) != 0;

        internal override bool hasNotNullConstraint
            => (_additionalFlags & TemplateMetadataWriter.TemplateParameterFlags.HasNotNullConstraint) != 0;

        internal override bool hasConstructorConstraint
            => (_flags & GenericParameterAttributes.DefaultConstructorConstraint) != 0;

        internal override bool allowsRefLikeType
            => (_flags & MetadataHelpers.GenericParameterAttributesAllowByRefLike) != 0;

        internal override bool hasDefaultConstraint
            => (_additionalFlags & TemplateMetadataWriter.TemplateParameterFlags.HasDefaultConstraint) != 0;

        internal override bool hasValueTypeConstraint
            => (_flags & GenericParameterAttributes.NotNullableValueTypeConstraint) != 0;

        internal override bool hasReferenceTypeConstraint
            => (_flags & GenericParameterAttributes.ReferenceTypeConstraint) != 0;

        internal override bool isReferenceTypeFromConstraintTypes
            => CalculateIsReferenceTypeFromConstraintTypes(constraintTypes);

        internal override bool isValueTypeFromConstraintTypes
            => CalculateIsValueTypeFromConstraintTypes(constraintTypes);

        internal override bool hasDefaultFromConstraintTypes
            => CalculateHasDefaultFromConstraintTypes(constraintTypes);

        internal override bool hasConstructorFromConstraintTypes
            => CalculateHasConstructorFromConstraintTypes(constraintTypes);

        internal override TextLocation location => locations[0];

        internal override SyntaxReference syntaxReference => null;

        internal override TypeWithAnnotations underlyingType => _underlyingType;

        internal override TypeOrConstant defaultValue => _defaultValue;

        internal override ImmutableArray<AttributeData> GetAttributes() {
            // TODO
            // if (_lazyCustomAttributes.IsDefault) {
            //     var containingPEModuleSymbol = (PEModuleSymbol)this.ContainingModule;

            //     var loadedCustomAttributes = containingPEModuleSymbol.GetCustomAttributesForToken(
            //         Handle,
            //         out _,
            //         // Filter out [IsUnmanagedAttribute]
            //         HasUnmanagedTypeConstraint ? AttributeDescription.IsUnmanagedAttribute : default);

            //     ImmutableInterlocked.InterlockedInitialize(ref _lazyCustomAttributes, loadedCustomAttributes);
            // }

            // return _lazyCustomAttributes;
            return [];
        }

        private ImmutableArray<TypeWithAnnotations> GetDeclaredConstraintTypes(
            ConsList<PETemplateParameterSymbol> inProgress) {
            // TODO
            return [];
        }

        internal override ImmutableArray<NamedTypeSymbol> GetInterfaces(ConsList<TemplateParameterSymbol> inProgress) {
            var bounds = GetBounds(inProgress);
            return (bounds is not null) ? bounds.interfaces : [];
        }

        internal override void EnsureConstraintsAreResolved() {
            if (!_lazyBounds.IsSet()) {
                var typeParameters = (_containingSymbol.kind == SymbolKind.Method)
                    ? ((MetadataMethodSymbol)_containingSymbol).templateParameters
                    : ((PETemplateType)_containingSymbol).templateParameters;

                EnsureConstraintsAreResolved(typeParameters);
            }
        }

        internal override ImmutableArray<TypeWithAnnotations> GetConstraintTypes(
            ConsList<TemplateParameterSymbol> inProgress) {
            var bounds = GetBounds(inProgress);
            return (bounds is not null) ? bounds.constraintTypes : [];
        }

        internal override NamedTypeSymbol GetEffectiveBaseClass(ConsList<TemplateParameterSymbol> inProgress) {
            var bounds = GetBounds(inProgress);
            return (bounds is not null) ? bounds.effectiveBaseClass : GetDefaultBaseType();
        }

        internal override TypeSymbol GetDeducedBaseType(ConsList<TemplateParameterSymbol> inProgress) {
            var bounds = GetBounds(inProgress);
            return (bounds is not null) ? bounds.deducedBaseType : GetDefaultBaseType();
        }

        private TypeParameterBounds GetBounds(ConsList<TemplateParameterSymbol> inProgress) {
            if (_lazyBounds == TypeParameterBounds.Unset) {
                var constraintTypes = GetDeclaredConstraintTypes(ConsList<PETemplateParameterSymbol>.Empty);
                var diagnostics = BelteDiagnosticQueue.GetInstance();

                var inherited = (_containingSymbol.kind == SymbolKind.Method) &&
                    ((MethodSymbol)_containingSymbol).isOverride;

                var bounds = this.ResolveBounds(
                    containingAssembly.corLibrary,
                    inProgress.Prepend(this),
                    constraintTypes,
                    inherited,
                    null,
                    diagnostics,
                    null
                );

                diagnostics.Free();
                Interlocked.CompareExchange(ref _lazyBounds, bounds, TypeParameterBounds.Unset);
            }

            return _lazyBounds;
        }

        private NamedTypeSymbol GetDefaultBaseType() {
            return containingAssembly.corLibrary.GetSpecialType(SpecialType.Object);
        }
    }
}
