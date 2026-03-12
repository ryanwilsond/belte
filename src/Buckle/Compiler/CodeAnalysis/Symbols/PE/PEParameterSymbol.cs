using System;
using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Threading;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Symbols;

internal partial class PEParameterSymbol : ParameterSymbol {
    // private static readonly ImmutableArray<int> DefaultStringHandlerAttributeIndexes = [int.MinValue];

    private readonly Symbol _containingSymbol;
    private readonly string _name;
    private readonly TypeWithAnnotations _typeWithAnnotations;
    private readonly ParameterHandle _handle;
    private readonly ParameterAttributes _flags;
    private readonly PEModuleSymbol _moduleSymbol;

    private ImmutableArray<AttributeData> _lazyCustomAttributes;
    private ConstantValue? _lazyDefaultValue = ConstantValue.Unset;

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

            if (value.HasValue) {
                typeWithAnnotations = NullableTypeDecoder.TransformType(
                    typeWithAnnotations,
                    value.GetValueOrDefault(),
                    default
                );
            }

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
                    // refKind = RefKind.Out;
                    // TODO no equiv
                } else if (!isReturn && moduleSymbol.module.HasRequiresLocationAttribute(handle)) {
                    refKind = RefKind.RefConstParameter;
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
            // typeWithAnnotations = TupleTypeDecoder.DecodeTupleTypesIfApplicable(typeWithAnnotations, handle, moduleSymbol);

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

    internal override TypeWithAnnotations typeWithAnnotations => _typeWithAnnotations;

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
        else if (parameter.refKind is /*RefKind.In or */RefKind.RefConstParameter)
            isBad |= isContainingSymbolVirtual != hasInAttributeModifier;
        else if (hasInAttributeModifier)
            isBad = true;

        return parameter;
    }

    internal override ImmutableArray<AttributeData> GetAttributes() {
        return [];
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
