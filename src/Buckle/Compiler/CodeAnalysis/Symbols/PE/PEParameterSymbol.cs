using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Metadata;
using System.Threading;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Symbols;

internal partial class PEParameterSymbol : ParameterSymbol {
    // private static readonly ImmutableArray<int> DefaultStringHandlerAttributeIndexes = [int.MinValue];

    private readonly Symbol _containingSymbol;
    private readonly string _name;
    private readonly TypeWithAnnotations _typeWithAnnotations;
    private readonly ParameterHandle _handle;
    private readonly ParameterAttributes _flags;
    private readonly PEModuleSymbol _moduleSymbol;

    private TypeWithAnnotations _lazyActualTypeWithAnnotations;
    private ImmutableArray<AttributeData> _lazyCustomAttributes;
    private ConstantValue? _lazyDefaultValue = ConstantValue.Unset;
    private int _lazyIsConst;

    // private ImmutableArray<int> _lazyInterpolatedStringHandlerAttributeIndexes = DefaultStringHandlerAttributeIndexes;

    // private int _lazyCallerArgumentExpressionParameterIndex = -2;
    private ImmutableArray<AttributeData> _lazyHiddenAttributes;

    private readonly ushort _ordinal;

    private PackedFlags _packedFlags;

    private PEParameterSymbol(
        PEModuleSymbol moduleSymbol,
        Symbol containingSymbol,
        int ordinal,
        bool isByRef,
        TypeWithAnnotations typeWithAnnotations,
        ParameterHandle handle,
        Symbol nullableContext,
        int countOfCustomModifiers,
        bool isReturn,
        out bool isBad) {
        isBad = false;
        _moduleSymbol = moduleSymbol;
        _containingSymbol = containingSymbol;
        _ordinal = (ushort)ordinal;

        _handle = handle;

        var refKind = RefKind.None;
        var scope = ScopedKind.None;
        var hasUnscopedRefAttribute = false;

        if (handle.IsNil) {
            refKind = isByRef ? RefKind.Ref : RefKind.None;
            var value = nullableContext.GetNullableContextValue();

            // Always transform?
            // if (value.HasValue) {
            typeWithAnnotations = NullableTypeDecoder.TransformType(
                typeWithAnnotations,
                value.GetValueOrDefault(),
                default,
                false
            );
            // }

            _lazyCustomAttributes = [];
            _lazyHiddenAttributes = [];
            _lazyDefaultValue = null;
        } else {
            try {
                moduleSymbol.module.GetParamPropsOrThrow(handle, out _name, out _flags);
            } catch (BadImageFormatException) {
                isBad = true;
            }

            if (isByRef) {
                var inOutFlags = _flags & (ParameterAttributes.Out | ParameterAttributes.In);

                if (inOutFlags == ParameterAttributes.Out) {
                    refKind = RefKind.Out;
                } else if (!isReturn && moduleSymbol.module.HasRequiresLocationAttribute(handle)) {
                    refKind = RefKind.RefConst;
                } else if (moduleSymbol.module.HasIsReadOnlyAttribute(handle)) {
                    // refKind = RefKind.In;
                } else {
                    refKind = RefKind.Ref;
                }
            }

            // var typeSymbol = DynamicTypeDecoder.TransformType(typeWithAnnotations.Type, countOfCustomModifiers, handle, moduleSymbol, refKind);
            // typeSymbol = NativeIntegerTypeDecoder.TransformType(typeSymbol, handle, moduleSymbol, containingSymbol.ContainingType);
            // typeWithAnnotations = typeWithAnnotations.WithTypeAndModifiers(typeSymbol, typeWithAnnotations.CustomModifiers);
            var accessSymbol = containingSymbol;

            typeWithAnnotations = NullableTypeDecoder.TransformType(
                typeWithAnnotations,
                handle,
                moduleSymbol,
                accessSymbol: accessSymbol,
                nullableContext: nullableContext
            );

            typeWithAnnotations = TupleTypeDecoder.DecodeTupleTypesIfApplicable(
                typeWithAnnotations,
                handle,
                moduleSymbol
            );

            hasUnscopedRefAttribute = _moduleSymbol.module.HasUnscopedRefAttribute(_handle);

            if (hasUnscopedRefAttribute) {
                if (_moduleSymbol.module.HasScopedRefAttribute(_handle))
                    isBad = true;

                scope = ScopedKind.None;
            } else if (_moduleSymbol.module.HasScopedRefAttribute(_handle)) {
                if (isByRef)
                    scope = ScopedKind.ScopedRef;
                else if (typeWithAnnotations.type.IsRefLikeOrAllowsRefLikeType())
                    scope = ScopedKind.ScopedValue;
                else
                    isBad = true;

            }
            // else if (ParameterHelpers.IsRefScopedByDefault(_moduleSymbol.useUpdatedEscapeRules, refKind)) {
            //     scope = ScopedKind.ScopedRef;
            // }
        }

        _typeWithAnnotations = typeWithAnnotations;
        var hasNameInMetadata = !string.IsNullOrEmpty(_name);

        if (!hasNameInMetadata)
            _name = "value";

        _packedFlags = new PackedFlags(
            refKind,
            attributesAreComplete: handle.IsNil,
            hasNameInMetadata: hasNameInMetadata,
            scope,
            hasUnscopedRefAttribute
        );
    }

    public override RefKind refKind => _packedFlags.refKind;

    public override string name => _name;

    public override string metadataName => _hasNameInMetadata ? _name : "";

    internal ParameterAttributes flags => _flags;

    public override int ordinal => _ordinal;

    internal ParameterHandle handle => _handle;

    internal override Symbol containingSymbol => _containingSymbol;

    internal bool hasMetadataConstantValue => (_flags & ParameterAttributes.HasDefault) != 0;

    private bool _hasNameInMetadata => _packedFlags.hasNameInMetadata;

    internal override ImmutableArray<TextLocation> locations => _containingSymbol.locations;

    internal override ImmutableArray<SyntaxReference> declaringSyntaxReferences => [];

    internal override SyntaxReference syntaxReference => null;

    internal override TextLocation location => locations[0];

    internal sealed override ScopedKind effectiveScope => _packedFlags.scope;

    internal override bool hasUnscopedRefAttribute => _packedFlags.hasUnscopedRefAttribute;

    internal bool useUpdatedEscapeRules => _moduleSymbol.useUpdatedEscapeRules;

    internal override bool isMetadataOptional => (_flags & ParameterAttributes.Optional) != 0;

    internal override bool isMetadataOut => (_flags & ParameterAttributes.Out) != 0;

    internal override bool isConst {
        get {
            _ = GetAttributes();
            Debug.Assert(_lazyIsConst != (int)ThreeState.Unknown);
            return _lazyIsConst == (int)ThreeState.True;
        }
    }

    internal override ConstantValue outDefaultValue => null;

    internal override TypeWithAnnotations typeWithAnnotations {
        get {
            if (_lazyActualTypeWithAnnotations is null) {
                var tentativeType = _typeWithAnnotations;

                // Special case where attribute constructor parameters are treated as non-nullable because it's separately
                // verified that this is the case
                // TODO The _hasNameInMetadata check ensures this isn't the return parameter (is this the best way to tell?)
                if (_hasNameInMetadata &&
                    tentativeType.isNullable &&
                    tentativeType.type.isReferenceType &&
                    ContainingMethodIsConstructor()) {
                    var attributeType = containingAssembly.GetTypeByMetadataName(
                        WellKnownType.System_Attribute.GetMetadataName(),
                        includeReferences: false,
                        useCLSCompliantNameArityEncoding: true,
                        isWellKnownType: true,
                        conflicts: out _
                    );

                    if (attributeType is not null &&
                        containingType.IsDerivedFrom(attributeType, TypeCompareKind.ConsiderEverything)) {
                        tentativeType = new TypeWithAnnotations(tentativeType.nullableUnderlyingTypeOrSelf);
                    }
                }

                Interlocked.CompareExchange(ref _lazyActualTypeWithAnnotations, tentativeType, null);
            }

            return _lazyActualTypeWithAnnotations;

            bool ContainingMethodIsConstructor() {
                var containingMethod = containingSymbol as MethodSymbol;

                if (containingMethod.hasSpecialName &&
                    containingMethod.name.StartsWith(".", StringComparison.Ordinal)) {
                    if (containingMethod.hasRuntimeSpecialName &&
                        !containingMethod.IsMetadataVirtual() &&
                        containingMethod.name.Equals(WellKnownMemberNames.InstanceConstructorName) &&
                        containingMethod.returnsVoid &&
                        containingMethod.arity == 0) {
                        return true;
                    }
                }

                return false;
            }
        }
    }

    internal override ConstantValue? explicitDefaultConstantValue {
        get {
            if (_lazyDefaultValue == ConstantValue.Unset) {
                var value = ImportConstantValue(ignoreAttributes: !isMetadataOptional);
                Interlocked.CompareExchange(ref _lazyDefaultValue, value, ConstantValue.Unset);
            }

            return _lazyDefaultValue;
        }
    }

    internal static PEParameterSymbol Create(
        PEModuleSymbol moduleSymbol,
        PEMethodSymbol containingSymbol,
        bool isContainingSymbolVirtual,
        int ordinal,
        ParamInfo<TypeSymbol> parameterInfo,
        Symbol nullableContext,
        bool isReturn,
        out bool isBad) {
        return Create(
            moduleSymbol,
            containingSymbol,
            isContainingSymbolVirtual,
            ordinal,
            parameterInfo.isByRef,
            parameterInfo.refCustomModifiers,
            parameterInfo.type,
            parameterInfo.handle,
            nullableContext,
            parameterInfo.customModifiers,
            isReturn,
            out isBad
        );
    }

    private static PEParameterSymbol Create(
        PEModuleSymbol moduleSymbol,
        Symbol containingSymbol,
        bool isContainingSymbolVirtual,
        int ordinal,
        bool isByRef,
        ImmutableArray<ModifierInfo<TypeSymbol>> refCustomModifiers,
        TypeSymbol type,
        ParameterHandle handle,
        Symbol nullableContext,
        ImmutableArray<ModifierInfo<TypeSymbol>> customModifiers,
        bool isReturn,
        out bool isBad) {
        // var typeWithModifiers = TypeWithAnnotations.Create(type, customModifiers: CSharpCustomModifier.Convert(customModifiers));
        var typeWithModifiers = new TypeWithAnnotations(type);

        var parameter = customModifiers.IsDefaultOrEmpty && refCustomModifiers.IsDefaultOrEmpty
            ? new PEParameterSymbol(
                moduleSymbol,
                containingSymbol,
                ordinal,
                isByRef,
                typeWithModifiers,
                handle,
                nullableContext,
                0,
                isReturn: isReturn,
                out isBad
            )
            : new PEParameterSymbolWithCustomModifiers(
                moduleSymbol,
                containingSymbol,
                ordinal,
                isByRef,
                refCustomModifiers,
                typeWithModifiers,
                handle,
                nullableContext,
                isReturn: isReturn,
                out isBad
            );

        // bool hasInAttributeModifier = parameter.refCustomModifiers.HasInAttributeModifier();
        var hasInAttributeModifier = false;

        if (isReturn)
            isBad |= parameter.refKind == RefKind.RefConst != hasInAttributeModifier;
        else if (parameter.refKind is /*RefKind.In or */RefKind.RefConst)
            isBad |= isContainingSymbolVirtual != hasInAttributeModifier;
        else if (hasInAttributeModifier)
            isBad = true;

        return parameter;
    }

    internal override ImmutableArray<AttributeData> GetAttributes() {
        // TODO Volatile read
        if (_lazyCustomAttributes.IsDefault) {
            var attributes = LoadAndFilterAttributes(
                out var hiddenAttributes,
                out var isParamArray,
                out var isParamCollection,
                out var isConst
            );

            ImmutableInterlocked.InterlockedInitialize(ref _lazyHiddenAttributes, hiddenAttributes);

            // if ((_lazyIsParams & IsParamsValues.Initialized) == 0) {
            //     IsParamsValues result = IsParamsValues.Initialized;

            //     if (isParamArray) {
            //         result |= IsParamsValues.Array;
            //     }

            //     if (isParamCollection) {
            //         result |= IsParamsValues.Collection;
            //     }

            //     Debug.Assert(_lazyIsParams == 0 || _lazyIsParams == result);
            //     _lazyIsParams = result;
            // }

            if (_lazyIsConst == (int)ThreeState.Unknown) {
                var val = isConst ? (int)ThreeState.True : (int)ThreeState.False;
                Interlocked.CompareExchange(ref _lazyIsConst, val, (int)ThreeState.Unknown);
            }

            ImmutableInterlocked.InterlockedInitialize(
                ref _lazyCustomAttributes,
                attributes
            );
        }

        Debug.Assert(!_lazyHiddenAttributes.IsDefault);
        return _lazyCustomAttributes;

        ImmutableArray<AttributeData> LoadAndFilterAttributes(
            out ImmutableArray<AttributeData> hiddenAttributes,
            out bool isParamArray,
            out bool isParamCollection,
            out bool isConst) {
            hiddenAttributes = [];
            isParamArray = false;
            isParamCollection = false;
            isConst = false;

            Debug.Assert(!_handle.IsNil);
            var containingModule = (PEModuleSymbol)this.containingModule;

            if (!containingModule.TryGetNonEmptyCustomAttributes(_handle, out var customAttributeHandles))
                return [];

            // var filterOutParamArrayAttribute = (_lazyIsParams & (IsParamsValues.Initialized | IsParamsValues.Array)) is 0 or (IsParamsValues.Initialized | IsParamsValues.Array);
            // var filterOutParamCollectionAttribute = (_lazyIsParams & (IsParamsValues.Initialized | IsParamsValues.Collection)) is 0 or (IsParamsValues.Initialized | IsParamsValues.Collection);

            var defaultValue = explicitDefaultConstantValue;
            var filterOutConstantAttributeDescription = default(AttributeDescription);

            // if (defaultValue is not null) {
            //     if (defaultValue.Discriminator == ConstantValueTypeDiscriminator.DateTime) {
            //         filterOutConstantAttributeDescription = AttributeDescription.DateTimeConstantAttribute;
            //     } else if (defaultValue.Discriminator == ConstantValueTypeDiscriminator.Decimal) {
            //         filterOutConstantAttributeDescription = AttributeDescription.DecimalConstantAttribute;
            //     }
            // }

            // var filterIsReadOnlyAttribute = this.refKind == RefKind.In;
            // bool filterRequiresLocationAttribute = this.RefKind == RefKind.RefReadOnlyParameter;

            using var builder = TemporaryArray<AttributeData>.Empty;
            CustomAttributeHandle paramArrayAttribute = default;
            CustomAttributeHandle paramCollectionAttribute = default;
            CustomAttributeHandle constantAttribute = default;

            foreach (var handle in customAttributeHandles) {
                // if (filterOutParamArrayAttribute && containingModule.AttributeMatchesFilter(handle, AttributeDescription.ParamArrayAttribute)) {
                //     paramArrayAttribute = handle;
                //     continue;
                // }

                // if (filterOutParamCollectionAttribute && containingModule.AttributeMatchesFilter(handle, AttributeDescription.ParamCollectionAttribute)) {
                //     paramCollectionAttribute = handle;
                //     continue;
                // }

                if (containingModule.AttributeMatchesFilter(handle, filterOutConstantAttributeDescription)) {
                    constantAttribute = handle;
                    continue;
                }

                // if (filterIsReadOnlyAttribute && containingModule.AttributeMatchesFilter(handle, AttributeDescription.IsReadOnlyAttribute))
                //     continue;

                // if (filterRequiresLocationAttribute && containingModule.AttributeMatchesFilter(handle, AttributeDescription.RequiresLocationAttribute))
                //     continue;

                if (containingModule.AttributeMatchesFilter(handle, AttributeDescription.ScopedRefAttribute))
                    continue;

                if (containingModule.AttributeMatchesFilter(handle, AttributeDescription.NullabilityAttribute))
                    continue;

                if (containingModule.AttributeMatchesFilter(handle, AttributeDescription.ConstParamAttribute)) {
                    isConst = true;
                    continue;
                }

                builder.Add(new PEAttributeData(containingModule, handle));
            }

            isParamArray = !paramArrayAttribute.IsNil;
            isParamCollection = !paramCollectionAttribute.IsNil;
            var hiddenCount = (isParamArray ? 1 : 0)
                + (!constantAttribute.IsNil ? 1 : 0)
                + (isParamCollection ? 1 : 0);

            if (hiddenCount != 0) {
                var hiddenBuilder = ArrayBuilder<AttributeData>.GetInstance(hiddenCount);

                if (isParamArray)
                    hiddenBuilder.Add(new PEAttributeData(containingModule, paramArrayAttribute));

                if (isParamCollection)
                    hiddenBuilder.Add(new PEAttributeData(containingModule, paramCollectionAttribute));

                if (!constantAttribute.IsNil)
                    hiddenBuilder.Add(new PEAttributeData(containingModule, constantAttribute));

                hiddenAttributes = hiddenBuilder.ToImmutableAndFree();
            }

            return builder.ToImmutableAndClear();
        }
    }

    internal ConstantValue? ImportConstantValue(bool ignoreAttributes = false) {
        ConstantValue? value = null;

        if ((_flags & ParameterAttributes.HasDefault) != 0)
            value = _moduleSymbol.module.GetParamDefaultValue(_handle);

        // if (value == null && !ignoreAttributes)
        //     value = GetDefaultDecimalOrDateTimeValue();

        return value;
    }
}
