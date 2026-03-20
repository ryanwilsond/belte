using System;
using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed partial class PEMethodSymbol : MethodSymbol {
    private readonly MethodDefinitionHandle _handle;
    private readonly string _name;
    private readonly PENamedTypeSymbol _containingType;
    private PackedFlags _packedFlags;
    private readonly ushort _flags;
    private readonly ushort _implFlags;
    private ImmutableArray<TemplateParameterSymbol> _lazyTypeParameters;
    private SignatureData _lazySignature;
    // private ImmutableArray<MethodSymbol> _lazyExplicitMethodImplementations;
    private UncommonFields _uncommonFields;

    internal PEMethodSymbol(
        PEModuleSymbol moduleSymbol,
        PENamedTypeSymbol containingType,
        MethodDefinitionHandle methodDef) {
        _handle = methodDef;
        _containingType = containingType;

        MethodAttributes localFlags = 0;

        try {
            moduleSymbol.module.GetMethodDefPropsOrThrow(
                methodDef,
                out _name,
                out var implFlags,
                out localFlags,
                out var rva
            );

            _implFlags = (ushort)implFlags;
        } catch (BadImageFormatException) {
            _name ??= "";
        }

        _flags = (ushort)localFlags;
    }

    public override int arity {
        get {
            if (!_lazyTypeParameters.IsDefault)
                return _lazyTypeParameters.Length;

            try {
                MetadataDecoder.GetSignatureCountsOrThrow(
                    _containingType.containingPEModule.module,
                    _handle,
                    out var parameterCount,
                    out var typeParameterCount
                );

                return typeParameterCount;
            } catch (BadImageFormatException) {
                return templateParameters.Length;
            }
        }
    }

    public override string name => _name;

    public override RefKind refKind => signature.returnParam.refKind;

    public override bool returnsVoid => returnType.IsVoidType();

    public override ImmutableArray<BoundExpression> templateConstraints => [];

    public override MethodKind methodKind {
        get {
            if (!_packedFlags.methodKindIsPopulated)
                _packedFlags.InitializeMethodKind(ComputeMethodKind());

            return _packedFlags.methodKind;
        }
    }

    public override ImmutableArray<TemplateParameterSymbol> templateParameters => EnsureTypeParametersAreLoaded();

    public override ImmutableArray<TypeOrConstant> templateArguments
        => isTemplateMethod ? GetTemplateParametersAsTemplateArguments() : [];

    internal override Symbol containingSymbol => _containingType;

    internal override NamedTypeSymbol containingType => _containingType;

    internal SignatureData signature => _lazySignature ?? LoadSignature();

    internal override ImmutableArray<ParameterSymbol> parameters => signature.parameters;

    internal PEParameterSymbol returnTypeParameter => signature.returnParam;

    internal override TypeWithAnnotations returnTypeWithAnnotations => signature.returnParam.typeWithAnnotations;

    internal override ModuleSymbol containingModule => _containingType.containingModule;

    internal MethodDefinitionHandle handle => _handle;

    internal override bool hasSpecialName => HasFlag(MethodAttributes.SpecialName);

    internal MethodAttributes flags => (MethodAttributes)_flags;

    internal override ImmutableArray<TextLocation> locations
        => _containingType.containingPEModule.metadataLocation.Cast<MetadataLocation, TextLocation>();

    internal override ImmutableArray<SyntaxReference> declaringSyntaxReferences => [];

    internal override TextLocation location => locations[0];

    internal override SyntaxReference syntaxReference => null;

    internal override bool isAbstract => HasFlag(MethodAttributes.Abstract);

    internal override bool isVirtual => IsMetadataVirtual() && !isMetadataFinal && !isAbstract && !isOverride;

    internal override bool isOverride
        => IsMetadataVirtual() &&
            !IsMetadataNewSlot() && _containingType.baseType is not null || _isExplicitClassOverride;

    internal override bool isStatic => HasFlag(MethodAttributes.Static);

    internal override bool hidesBaseMethodsByName => !HasFlag(MethodAttributes.HideBySig);

    // TODO Correct?
    internal override CallingConvention callingConvention => (CallingConvention)signature.header.RawValue;

    internal override Accessibility declaredAccessibility {
        get {
            return (object)(flags & MethodAttributes.MemberAccessMask) switch {
                MethodAttributes.Assembly => Accessibility.Private,// return Accessibility.Internal;
                MethodAttributes.FamORAssem => Accessibility.Private,// return Accessibility.ProtectedOrInternal;
                MethodAttributes.FamANDAssem => Accessibility.Private,// return Accessibility.ProtectedAndInternal;
                MethodAttributes.Private or MethodAttributes.PrivateScope => Accessibility.Private,
                MethodAttributes.Public => Accessibility.Public,
                MethodAttributes.Family => Accessibility.Protected,
                _ => Accessibility.Private,
            };
        }
    }

    internal override OverriddenOrHiddenMembersResult overriddenOrHiddenMembers {
        get {
            if (!_packedFlags.isOverriddenOrHiddenMembersPopulated) {
                var result = base.overriddenOrHiddenMembers;

                if (result != OverriddenOrHiddenMembersResult.Empty) {
                    result = InterlockedOperations.Initialize(
                        ref AccessUncommonFields()._lazyOverriddenOrHiddenMembersResult,
                        result
                    );
                }

                _packedFlags.SetIsOverriddenOrHiddenMembersPopulated();
                return result;
            }

            var uncommonFields = _uncommonFields;

            if (uncommonFields is null)
                return OverriddenOrHiddenMembersResult.Empty;

            return uncommonFields._lazyOverriddenOrHiddenMembersResult
                ?? InterlockedOperations.Initialize(
                    ref uncommonFields._lazyOverriddenOrHiddenMembersResult,
                    OverriddenOrHiddenMembersResult.Empty
                );
        }
    }

    internal sealed override bool hasUnscopedRefAttribute {
        get {
            if (!_packedFlags.isUnscopedRefPopulated) {
                var moduleSymbol = _containingType.containingPEModule;
                var unscopedRef = moduleSymbol.module.HasUnscopedRefAttribute(_handle);
                _packedFlags.InitializeIsUnscopedRef(unscopedRef);
            }

            return _packedFlags.isUnscopedRef;
        }
    }

    internal override bool isDeclaredConst {
        get {
            if (!_packedFlags.isReadOnlyPopulated) {
                var isReadOnly = false;

                if (_isValidReadOnlyTarget) {
                    var moduleSymbol = _containingType.containingPEModule;
                    isReadOnly = moduleSymbol.module.HasIsReadOnlyAttribute(_handle);
                }

                _packedFlags.InitializeIsReadOnly(isReadOnly);
            }
            return _packedFlags.isReadOnly;
        }
    }

    internal override bool isSealed => isMetadataFinal && !isAbstract && isOverride;

    internal override bool isMetadataFinal => HasFlag(MethodAttributes.Final);

    internal override int parameterCount {
        get {
            if (_lazySignature is not null)
                return _lazySignature.parameters.Length;

            try {
                MetadataDecoder.GetSignatureCountsOrThrow(
                    _containingType.containingPEModule.module,
                    _handle,
                    out var parameterCount,
                    out var typeParameterCount
                );

                return parameterCount;
            } catch (BadImageFormatException) {
                return parameters.Length;
            }
        }
    }

    private bool _isExplicitClassOverride {
        get {
            if (!_packedFlags.isExplicitOverrideIsPopulated) {
                // var unused = this.explicitInterfaceImplementations;
            }

            return _packedFlags.isExplicitClassOverride;
        }
    }

    internal override ImmutableArray<AttributeData> GetAttributes() {
        // TODO
        return [];
    }

    internal override ImmutableArray<AttributeData> GetReturnTypeAttributes() {
        return signature.returnParam.GetAttributes();
    }

    internal override byte? GetNullableContextValue() {
        if (!_packedFlags.TryGetNullableContext(out var value)) {
            value = _containingType.containingPEModule.module.HasNullableContextAttribute(_handle, out var arg)
                ? arg
                : _containingType.GetNullableContextValue();

            _packedFlags.SetNullableContext(value);
        }

        return value;
    }

    private bool _isValidReadOnlyTarget
        => !isStatic && containingType.IsStructType() && methodKind != MethodKind.Constructor;

    private bool HasFlag(MethodAttributes flag) {
        return ((ushort)flag & _flags) != 0;
    }

    internal override byte? GetLocalNullableContextValue() {
        throw ExceptionUtilities.Unreachable();
    }

    internal override bool IsMetadataVirtual(bool forceComplete = false) => HasFlag(MethodAttributes.Virtual);

    internal bool IsMetadataNewSlot(bool ignoreInterfaceImplementationChanges = false)
        => HasFlag(MethodAttributes.NewSlot);

    private UncommonFields AccessUncommonFields() {
        var retVal = _uncommonFields;
        return retVal ?? InterlockedOperations.Initialize(ref _uncommonFields, CreateUncommonFields());

        UncommonFields CreateUncommonFields() {
            var retVal = new UncommonFields();

            if (_packedFlags.isCustomAttributesPopulated)
                retVal._lazyCustomAttributes = [];

            if (_packedFlags.isConditionalPopulated)
                retVal._lazyConditionalAttributeSymbols = [];

            if (_packedFlags.isOverriddenOrHiddenMembersPopulated)
                retVal._lazyOverriddenOrHiddenMembersResult = OverriddenOrHiddenMembersResult.Empty;

            if (_packedFlags.isMemberNotNullPopulated) {
                retVal._lazyNotNullMembers = [];
                retVal._lazyNotNullMembersWhenTrue = [];
                retVal._lazyNotNullMembersWhenFalse = [];
            }

            if (_packedFlags.isExplicitOverrideIsPopulated)
                retVal._lazyExplicitClassOverride = null;

            return retVal;
        }
    }

    private SignatureData LoadSignature() {
        var moduleSymbol = _containingType.containingPEModule;

        var paramInfo = new MetadataDecoder(moduleSymbol, this)
            .GetSignatureForMethod(_handle, out var signatureHeader, out var mrEx);

        var makeBad = mrEx != null;

        if (!signatureHeader.IsGeneric && _lazyTypeParameters.IsDefault)
            ImmutableInterlocked.InterlockedInitialize(ref _lazyTypeParameters, []);

        var count = paramInfo.Length - 1;
        ImmutableArray<ParameterSymbol> @params;
        bool isBadParameter;

        if (count > 0) {
            var builder = ImmutableArray.CreateBuilder<ParameterSymbol>(count);

            for (var i = 0; i < count; i++) {
                builder.Add(PEParameterSymbol.Create(
                    moduleSymbol, this, IsMetadataVirtual(), i,
                    paramInfo[i + 1], nullableContext: this, isReturn: false, out isBadParameter)
                );

                if (isBadParameter)
                    makeBad = true;
            }

            @params = builder.ToImmutable();
        } else {
            @params = [];
        }

        // var returnType = paramInfo[0].type.AsDynamicIfNoPia(_containingType);
        // paramInfo[0].type = returnType;

        var returnParam = PEParameterSymbol.Create(
            moduleSymbol, this, IsMetadataVirtual(), 0,
            paramInfo[0], nullableContext: this, isReturn: true, out isBadParameter
        );

        var signature = new SignatureData(signatureHeader, @params, returnParam);
        return InterlockedOperations.Initialize(ref _lazySignature, signature);
    }

    private MethodKind ComputeMethodKind() {
        if (hasSpecialName) {
            // if (_name.StartsWith(".", StringComparison.Ordinal)) {
            //     if ((Flags & (MethodAttributes.RTSpecialName | MethodAttributes.Virtual)) == MethodAttributes.RTSpecialName &&
            //         _name.Equals(this.IsStatic ? WellKnownMemberNames.StaticConstructorName : WellKnownMemberNames.InstanceConstructorName) &&
            //         this.ReturnsVoid && this.Arity == 0) {
            //         if (this.IsStatic) {
            //             if (Parameters.Length == 0) {
            //                 return MethodKind.StaticConstructor;
            //             }
            //         } else {
            //             return MethodKind.Constructor;
            //         }
            //     }

            //     return MethodKind.Ordinary;
            // }

            // if (!this.HasRuntimeSpecialName && this.IsStatic && this.DeclaredAccessibility == Accessibility.Public) {
            //     switch (_name) {
            //         case WellKnownMemberNames.CheckedAdditionOperatorName:
            //         case WellKnownMemberNames.AdditionOperatorName:
            //         case WellKnownMemberNames.BitwiseAndOperatorName:
            //         case WellKnownMemberNames.BitwiseOrOperatorName:
            //         case WellKnownMemberNames.CheckedDivisionOperatorName:
            //         case WellKnownMemberNames.DivisionOperatorName:
            //         case WellKnownMemberNames.EqualityOperatorName:
            //         case WellKnownMemberNames.ExclusiveOrOperatorName:
            //         case WellKnownMemberNames.GreaterThanOperatorName:
            //         case WellKnownMemberNames.GreaterThanOrEqualOperatorName:
            //         case WellKnownMemberNames.InequalityOperatorName:
            //         case WellKnownMemberNames.LeftShiftOperatorName:
            //         case WellKnownMemberNames.LessThanOperatorName:
            //         case WellKnownMemberNames.LessThanOrEqualOperatorName:
            //         case WellKnownMemberNames.ModulusOperatorName:
            //         case WellKnownMemberNames.CheckedMultiplyOperatorName:
            //         case WellKnownMemberNames.MultiplyOperatorName:
            //         case WellKnownMemberNames.RightShiftOperatorName:
            //         case WellKnownMemberNames.UnsignedRightShiftOperatorName:
            //         case WellKnownMemberNames.CheckedSubtractionOperatorName:
            //         case WellKnownMemberNames.SubtractionOperatorName:
            //             return IsValidUserDefinedOperatorSignature(2) ? MethodKind.UserDefinedOperator : MethodKind.Ordinary;
            //         case WellKnownMemberNames.CheckedDecrementOperatorName:
            //         case WellKnownMemberNames.DecrementOperatorName:
            //         case WellKnownMemberNames.FalseOperatorName:
            //         case WellKnownMemberNames.CheckedIncrementOperatorName:
            //         case WellKnownMemberNames.IncrementOperatorName:
            //         case WellKnownMemberNames.LogicalNotOperatorName:
            //         case WellKnownMemberNames.OnesComplementOperatorName:
            //         case WellKnownMemberNames.TrueOperatorName:
            //         case WellKnownMemberNames.CheckedUnaryNegationOperatorName:
            //         case WellKnownMemberNames.UnaryNegationOperatorName:
            //         case WellKnownMemberNames.UnaryPlusOperatorName:
            //             return IsValidUserDefinedOperatorSignature(1) ? MethodKind.UserDefinedOperator : MethodKind.Ordinary;
            //         case WellKnownMemberNames.ImplicitConversionName:
            //         case WellKnownMemberNames.ExplicitConversionName:
            //         case WellKnownMemberNames.CheckedExplicitConversionName:
            //             return IsValidUserDefinedOperatorSignature(1) ? MethodKind.Conversion : MethodKind.Ordinary;

            //             //case WellKnownMemberNames.ConcatenateOperatorName:
            //             //case WellKnownMemberNames.ExponentOperatorName:
            //             //case WellKnownMemberNames.IntegerDivisionOperatorName:
            //             //case WellKnownMemberNames.LikeOperatorName:
            //             //// Non-C#-supported overloaded operator
            //             //return MethodKind.Ordinary;
            //     }

            //     return MethodKind.Ordinary;
            // }
            // TODO
        }

        return MethodKind.Ordinary;
    }

    private ImmutableArray<TemplateParameterSymbol> EnsureTypeParametersAreLoaded() {
        var typeParams = _lazyTypeParameters;

        if (!typeParams.IsDefault)
            return typeParams;

        return InterlockedOperations.Initialize(ref _lazyTypeParameters, LoadTypeParameters());
    }

    private ImmutableArray<TemplateParameterSymbol> LoadTypeParameters() {
        try {
            var moduleSymbol = _containingType.containingPEModule;
            var gpHandles = moduleSymbol.module.GetGenericParametersForMethodOrThrow(_handle);

            if (gpHandles.Count == 0) {
                return [];
            } else {
                var ownedParams = ImmutableArray.CreateBuilder<TemplateParameterSymbol>(gpHandles.Count);

                for (var i = 0; i < gpHandles.Count; i++)
                    ownedParams.Add(new PETemplateParameterSymbol(moduleSymbol, this, (ushort)i, gpHandles[i]));

                return ownedParams.ToImmutable();
            }
        } catch (BadImageFormatException) {
            return [];
        }
    }

    internal override int CalculateLocalSyntaxOffset(int localPosition, SyntaxTree localTree) {
        throw ExceptionUtilities.Unreachable();
    }
}
