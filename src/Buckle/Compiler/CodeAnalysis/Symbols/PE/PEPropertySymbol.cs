using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Threading;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Symbols;

internal partial class PEPropertySymbol : PropertySymbol {
    private readonly string _name;
    private readonly PENamedTypeSymbol _containingType;
    private readonly ImmutableArray<ParameterSymbol> _parameters;
    private readonly RefKind _refKind;
    private readonly TypeWithAnnotations _propertyTypeWithAnnotations;
    private readonly PEMethodSymbol _getMethod;
    private readonly PEMethodSymbol _setMethod;
    private UncommonFields _uncommonFields;

    private const int UnsetAccessibility = -1;
    private int _declaredAccessibility = UnsetAccessibility;

    private PackedFlags _flags;

    internal static PEPropertySymbol Create(
        PEModuleSymbol moduleSymbol,
        PENamedTypeSymbol containingType,
        PropertyDefinitionHandle handle,
        PEMethodSymbol getMethod,
        PEMethodSymbol setMethod) {
        Debug.Assert(moduleSymbol is not null);
        Debug.Assert(containingType is not null);
        Debug.Assert(!handle.IsNil);

        var metadataDecoder = new MetadataDecoder(moduleSymbol, containingType);
        var propertyParams = metadataDecoder.GetSignatureForProperty(handle, out var callingConvention, out var propEx);
        Debug.Assert(propertyParams.Length > 0);

        var returnInfo = propertyParams[0];

        var result = returnInfo.customModifiers.IsDefaultOrEmpty && returnInfo.refCustomModifiers.IsDefaultOrEmpty
            ? new PEPropertySymbol(
                moduleSymbol,
                containingType,
                handle,
                getMethod,
                setMethod,
                propertyParams,
                metadataDecoder
            )
            : new PEPropertySymbolWithCustomModifiers(
                moduleSymbol,
                containingType,
                handle,
                getMethod,
                setMethod,
                propertyParams,
                metadataDecoder
            );

        // var isBad = result.RefKind == RefKind.In != result.RefCustomModifiers.HasInAttributeModifier();

        // if (propEx is not null/* || isBad*/) {
        // result.AccessUncommonFields()._lazyCachedUseSiteInfo.Initialize(new CSDiagnosticInfo(ErrorCode.ERR_BindToBogus, result));
        // result._flags.SetUseSiteDiagnosticPopulated();
        // }

        return result;
    }

    private PEPropertySymbol(
        PEModuleSymbol moduleSymbol,
        PENamedTypeSymbol containingType,
        PropertyDefinitionHandle handle,
        PEMethodSymbol getMethod,
        PEMethodSymbol setMethod,
        ParamInfo<TypeSymbol>[] propertyParams,
        MetadataDecoder metadataDecoder) {
        _containingType = containingType;
        var module = moduleSymbol.module;
        PropertyAttributes mdFlags = 0;
        BadImageFormatException mrEx = null;

        try {
            module.GetPropertyDefPropsOrThrow(handle, out _name, out mdFlags);
        } catch (BadImageFormatException e) {
            mrEx = e;
            _name ??= string.Empty;
        }

        _getMethod = getMethod;
        _setMethod = setMethod;
        this.handle = handle;

        BadImageFormatException getEx = null;
        var getMethodParams = getMethod is null
            ? null
            : metadataDecoder.GetSignatureForMethod(getMethod.handle, out var unusedCallingConvention, out getEx);
        BadImageFormatException setEx = null;
        var setMethodParams = setMethod is null
            ? null
            : metadataDecoder.GetSignatureForMethod(setMethod.handle, out unusedCallingConvention, out setEx);

        _parameters = setMethodParams is null
            ? GetParameters(moduleSymbol, this, getMethod, propertyParams, getMethodParams, out var isBad)
            : GetParameters(moduleSymbol, this, setMethod, propertyParams, setMethodParams, out isBad);

        Debug.Assert(!_parameters.IsDefault);

        if (getEx is not null || setEx is not null || mrEx is not null || isBad) {
            // AccessUncommonFields()._lazyCachedUseSiteInfo.Initialize(new CSDiagnosticInfo(ErrorCode.ERR_BindToBogus, this));
            // _flags.SetUseSiteDiagnosticPopulated();
        }

        var returnInfo = propertyParams[0];
        // var typeCustomModifiers = CSharpCustomModifier.Convert(returnInfo.CustomModifiers);

        if (returnInfo.isByRef) {
            if (moduleSymbol.module.HasIsReadOnlyAttribute(handle))
                _refKind = RefKind.RefConst;
            else
                _refKind = RefKind.Ref;
        } else {
            _refKind = RefKind.None;
        }

        var originalPropertyType = returnInfo.type;

        // originalPropertyType = DynamicTypeDecoder.TransformType(originalPropertyType, typeCustomModifiers.Length, handle, moduleSymbol, _refKind);
        // originalPropertyType = NativeIntegerTypeDecoder.TransformType(originalPropertyType, handle, moduleSymbol, _containingType);

        // originalPropertyType = originalPropertyType.AsDynamicIfNoPia(_containingType);

        // We start without annotation (they will be decoded below)
        var propertyTypeWithAnnotations = new TypeWithAnnotations(originalPropertyType);

        propertyTypeWithAnnotations = NullableTypeDecoder.TransformType(
            propertyTypeWithAnnotations,
            handle,
            moduleSymbol,
            accessSymbol: _containingType,
            nullableContext: _containingType
        );

        propertyTypeWithAnnotations = TupleTypeDecoder.DecodeTupleTypesIfApplicable(
            propertyTypeWithAnnotations,
            handle,
            moduleSymbol
        );

        _propertyTypeWithAnnotations = propertyTypeWithAnnotations;

        var callMethodsDirectly = !DoSignaturesMatch(
                module,
                metadataDecoder,
                propertyParams,
                _getMethod,
                getMethodParams,
                _setMethod,
                setMethodParams
            ) ||
                MustCallMethodsDirectlyCore() ||
                AnyUnexpectedRequiredModifiers(propertyParams);

        if (!callMethodsDirectly) {
            _getMethod?.SetAssociatedProperty(this, MethodKind.PropertyGet);
            _setMethod?.SetAssociatedProperty(this, MethodKind.PropertySet);
        }

        _flags = new PackedFlags(
            isSpecialName: (mdFlags & PropertyAttributes.SpecialName) != 0,
            isRuntimeSpecialName: (mdFlags & PropertyAttributes.RTSpecialName) != 0,
            callMethodsDirectly
        );

        static bool AnyUnexpectedRequiredModifiers(ParamInfo<TypeSymbol>[] propertyParams) {
            return propertyParams.Any(p => (
                !p.refCustomModifiers.IsDefaultOrEmpty &&
                p.refCustomModifiers.Any(static m => !m.isOptional && !m.modifier.IsWellKnownTypeInAttribute())) ||
                p.customModifiers.AnyRequired()
            );
        }
    }

    internal override Symbol containingSymbol => _containingType;

    internal override NamedTypeSymbol containingType => _containingType;

    public override string name => /*this.IsIndexer ? WellKnownMemberNames.Indexer : */_name;

    internal override bool hasSpecialName => _flags.isSpecialName;

    public override string metadataName => _name;

    internal PropertyDefinitionHandle handle { get; }

    internal override Accessibility declaredAccessibility {
        get {
            if (_declaredAccessibility == UnsetAccessibility) {
                Accessibility accessibility;

                if (isOverride) {
                    var crossedAssemblyBoundaryWithoutInternalsVisibleTo = false;
                    var getAccessibility = Accessibility.NotApplicable;
                    var setAccessibility = Accessibility.NotApplicable;
                    PropertySymbol curr = this;

                    while (true) {
                        if (getAccessibility == Accessibility.NotApplicable) {
                            var getMethod = curr.getMethod;

                            if (getMethod is not null) {
                                var overriddenAccessibility = getMethod.declaredAccessibility;

                                getAccessibility = overriddenAccessibility == Accessibility.InternalOrProtected &&
                                    crossedAssemblyBoundaryWithoutInternalsVisibleTo
                                        ? Accessibility.Protected
                                        : overriddenAccessibility;
                            }
                        }

                        if (setAccessibility == Accessibility.NotApplicable) {
                            var setMethod = curr.setMethod;

                            if (setMethod is not null) {
                                var overriddenAccessibility = setMethod.declaredAccessibility;
                                setAccessibility = overriddenAccessibility == Accessibility.InternalOrProtected &&
                                    crossedAssemblyBoundaryWithoutInternalsVisibleTo
                                        ? Accessibility.Protected
                                        : overriddenAccessibility;
                            }
                        }

                        if (getAccessibility != Accessibility.NotApplicable &&
                            setAccessibility != Accessibility.NotApplicable) {
                            break;
                        }

                        var next = curr.overriddenProperty;

                        if (next is null)
                            break;

                        if (!crossedAssemblyBoundaryWithoutInternalsVisibleTo &&
                            !curr.containingAssembly.HasInternalAccessTo(next.containingAssembly)) {
                            crossedAssemblyBoundaryWithoutInternalsVisibleTo = true;
                        }

                        curr = next;
                    }

                    accessibility = PEPropertyOrEventHelpers.GetDeclaredAccessibilityFromAccessors(
                        getAccessibility,
                        setAccessibility
                    );
                } else {
                    accessibility = PEPropertyOrEventHelpers.GetDeclaredAccessibilityFromAccessors(
                        getMethod,
                        setMethod
                    );
                }

                Interlocked.CompareExchange(ref _declaredAccessibility, (int)accessibility, UnsetAccessibility);
            }

            return (Accessibility)_declaredAccessibility;
        }
    }

    // TODO
    internal override CallingConvention callingConvention => CallingConvention.Default;

    internal override bool isExtern
        => (_getMethod is not null && _getMethod.isExtern) || (_setMethod is not null && _setMethod.isExtern);

    internal override bool isAbstract
        => (_getMethod is not null && _getMethod.isAbstract) || (_setMethod is not null && _setMethod.isAbstract);

    internal override bool isSealed
        => (_getMethod is null || _getMethod.isSealed) && (_setMethod is null || _setMethod.isSealed);

    internal override bool isVirtual {
        get {
            return !isOverride && !isAbstract &&
                ((_getMethod is not null && _getMethod.isVirtual) || (_setMethod is not null && _setMethod.isVirtual));
        }
    }

    internal override bool isOverride
        => (_getMethod is not null && _getMethod.isOverride) || (_setMethod is not null && _setMethod.isOverride);

    internal override bool isStatic
        => (_getMethod is null || _getMethod.isStatic) && (_setMethod is null || _setMethod.isStatic);

    internal override ImmutableArray<ParameterSymbol> parameters => _parameters;

    internal override RefKind refKind => _refKind;

    internal override TypeWithAnnotations typeWithAnnotations => _propertyTypeWithAnnotations;

    internal override MethodSymbol getMethod => _getMethod;

    internal override MethodSymbol setMethod => _setMethod;

    internal override ImmutableArray<TextLocation> locations
        => _containingType.containingPEModule.metadataLocation.Cast<MetadataLocation, TextLocation>();

    internal override TextLocation location => locations[0];

    internal override ImmutableArray<SyntaxReference> declaringSyntaxReferences => [];

    internal override SyntaxReference syntaxReference => null;

    internal override ImmutableArray<AttributeData> GetAttributes() {
        if (!_flags.isCustomAttributesPopulated) {
            var attributes = LoadAndFilterAttributes(
                out var hasRequiredMemberAttribute,
                out var hasRequiresUnsafeAttribute
            );

            if (!attributes.IsEmpty) {
                ImmutableInterlocked.InterlockedInitialize(
                    ref AccessUncommonFields()._lazyCustomAttributes,
                    attributes
                );
            }

            // _flags.SetHasRequiredMemberAttribute(hasRequiredMemberAttribute);
            // _flags.SetRequiresUnsafe(ComputeRequiresUnsafe(hasRequiresUnsafeAttribute));
            _flags.SetCustomAttributesPopulated();
        }

        var uncommonFields = _uncommonFields;
        if (uncommonFields == null) {
            return [];
        } else {
            var result = uncommonFields._lazyCustomAttributes;

            if (result.IsDefault) {
                result = [];
                ImmutableInterlocked.InterlockedInitialize(ref uncommonFields._lazyCustomAttributes, result);
            }

            return result;
        }

        ImmutableArray<AttributeData> LoadAndFilterAttributes(
            out bool hasRequiredMemberAttribute,
            out bool hasRequiresUnsafeAttribute) {
            hasRequiredMemberAttribute = false;
            hasRequiresUnsafeAttribute = false;

            var containingModule = (PEModuleSymbol)this.containingModule;

            if (!containingModule.TryGetNonEmptyCustomAttributes(handle, out var customAttributeHandles))
                return [];

            var filterIsReadOnlyAttribute = refKind == RefKind.RefConst;

            using var builder = TemporaryArray<AttributeData>.Empty;

            foreach (var handle in customAttributeHandles) {
                if (filterIsReadOnlyAttribute &&
                    containingModule.AttributeMatchesFilter(handle, AttributeDescription.IsReadOnlyAttribute)) {
                    continue;
                }

                builder.Add(new PEAttributeData(containingModule, handle));
            }

            return builder.ToImmutableAndClear();
        }
    }

    internal override ImmutableArray<PropertySymbol> explicitInterfaceImplementations {
        get {
            if ((_getMethod is null || _getMethod.explicitInterfaceImplementations.Length == 0) &&
                (_setMethod is null || _setMethod.explicitInterfaceImplementations.Length == 0)) {
                return [];
            }

            var propertiesWithImplementedGetters =
                PEPropertyOrEventHelpers.GetPropertiesForExplicitlyImplementedAccessor(_getMethod);
            var propertiesWithImplementedSetters =
                PEPropertyOrEventHelpers.GetPropertiesForExplicitlyImplementedAccessor(_setMethod);

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

    internal override bool mustCallMethodsDirectly => _flags.callMethodsDirectly;

    private static bool DoSignaturesMatch(
        PEModule module,
        MetadataDecoder metadataDecoder,
        ParamInfo<TypeSymbol>[] propertyParams,
        PEMethodSymbol getMethod,
        ParamInfo<TypeSymbol>[] getMethodParams,
        PEMethodSymbol setMethod,
        ParamInfo<TypeSymbol>[] setMethodParams) {
        Debug.Assert(getMethodParams == null == (getMethod is null));
        Debug.Assert(setMethodParams == null == (setMethod is null));

        var hasGetMethod = getMethodParams is not null;
        var hasSetMethod = setMethodParams is not null;

        if (hasGetMethod && !metadataDecoder.DoPropertySignaturesMatch(
                propertyParams,
                getMethodParams,
                comparingToSetter: false,
                compareParamByRef: true,
                compareReturnType: true)) {
            return false;
        }

        if (hasSetMethod && !metadataDecoder.DoPropertySignaturesMatch(
                propertyParams,
                setMethodParams,
                comparingToSetter: true,
                compareParamByRef: true,
                compareReturnType: true)) {
            return false;
        }

        if (hasGetMethod && hasSetMethod) {
            if ((getMethod.isExtern != setMethod.isExtern) ||
                (getMethod.isSealed != setMethod.isSealed) ||
                (getMethod.isOverride != setMethod.isOverride) ||
                (getMethod.isStatic != setMethod.isStatic)) {
                return false;
            }
        }

        return true;
    }

    private static ImmutableArray<ParameterSymbol> GetParameters(
        PEModuleSymbol moduleSymbol,
        PEPropertySymbol property,
        PEMethodSymbol accessor,
        ParamInfo<TypeSymbol>[] propertyParams,
        ParamInfo<TypeSymbol>[] accessorParams,
        out bool anyParameterIsBad) {
        anyParameterIsBad = false;

        if (propertyParams.Length < 2)
            return [];

        var numAccessorParams = accessorParams.Length;

        var parameters = new ParameterSymbol[propertyParams.Length - 1];

        for (var i = 1; i < propertyParams.Length; i++) {
            var propertyParam = propertyParams[i];
            ParameterHandle paramHandle;
            Symbol nullableContext;

            if (i < numAccessorParams) {
                paramHandle = accessorParams[i].handle;
                nullableContext = accessor;
            } else {
                paramHandle = propertyParam.handle;
                nullableContext = property;
            }

            var ordinal = i - 1;

            parameters[ordinal] = PEParameterSymbol.Create(
                moduleSymbol,
                property,
                accessor.IsMetadataVirtual(),
                ordinal,
                paramHandle,
                propertyParam,
                nullableContext,
                out var isBad
            );

            if (isBad)
                anyParameterIsBad = true;
        }

        return parameters.AsImmutableOrNull();
    }

    internal sealed override Compilation declaringCompilation => null;

    private UncommonFields AccessUncommonFields() {
        var retVal = _uncommonFields;
        return retVal ?? InterlockedOperations.Initialize(ref _uncommonFields, CreateUncommonFields());

        UncommonFields CreateUncommonFields() {
            var retVal = new UncommonFields();
            // if (!_flags.IsObsoleteAttributePopulated) {
            //     retVal._lazyObsoleteAttributeData = ObsoleteAttributeData.Uninitialized;
            // }

            // if (!_flags.IsUseSiteDiagnosticPopulated) {
            //     retVal._lazyCachedUseSiteInfo = CachedUseSiteInfo<AssemblySymbol>.Uninitialized;
            // }

            if (_flags.isCustomAttributesPopulated)
                retVal._lazyCustomAttributes = [];

            return retVal;
        }
    }

    private bool MustCallMethodsDirectlyCore() {
        if (refKind != RefKind.None && _setMethod is not null) {
            return true;
        } else if (parameterCount == 0) {
            return false;
            // } else if (this.isIndexedProperty) {
            //     return this.IsStatic;
            // } else if (this.IsIndexer) {
            //     return this.HasRefOrOutParameter();
        } else {
            return true;
        }
    }
}
