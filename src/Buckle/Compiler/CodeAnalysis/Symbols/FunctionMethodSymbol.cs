using System;
using System.Collections.Immutable;
using System.Linq;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class FunctionMethodSymbol : MethodSymbol {
    private readonly ImmutableArray<FunctionParameterSymbol> _parameters;

    private FunctionMethodSymbol(
        RefKind refKind,
        TypeWithAnnotations returnType,
        ImmutableArray<ParameterSymbol> originalParameters,
        ImmutableArray<TypeOrConstant> substitutedParameterTypes) {
        this.refKind = refKind;
        returnTypeWithAnnotations = returnType;

        if (originalParameters.Length > 0) {
            var paramsBuilder = ArrayBuilder<FunctionParameterSymbol>.GetInstance(originalParameters.Length);

            for (var i = 0; i < originalParameters.Length; i++) {
                var originalParam = originalParameters[i];
                var substitutedType = substitutedParameterTypes[i];

                paramsBuilder.Add(new FunctionParameterSymbol(
                    substitutedType.type,
                    originalParam.refKind,
                    originalParam.ordinal,
                    containingSymbol: this
                ));
            }

            _parameters = paramsBuilder.ToImmutableAndFree();
        } else {
            _parameters = [];
        }
    }

    private FunctionMethodSymbol(ImmutableArray<ParamInfo<TypeSymbol>> retAndParamTypes) {
        var retInfo = retAndParamTypes[0];
        // var returnType = new TypeWithAnnotations(retInfo.type, customModifiers: CSharpCustomModifier.Convert(retInfo.CustomModifiers));
        var returnType = new TypeWithAnnotations(retInfo.type);

        // RefCustomModifiers = CSharpCustomModifier.Convert(retInfo.RefCustomModifiers);
        returnTypeWithAnnotations = returnType;
        // refKind = getRefKind(retInfo, RefCustomModifiers, RefKind.RefConst, RefKind.Ref, requiresLocationAllowed: false);
        refKind = retInfo.isByRef ? RefKind.Ref : RefKind.None;
        // UseUpdatedEscapeRules = useUpdatedEscapeRules;
        _parameters = MakeParametersFromMetadata(retAndParamTypes.AsSpan()[1..], this);

        static ImmutableArray<FunctionParameterSymbol> MakeParametersFromMetadata(
            ReadOnlySpan<ParamInfo<TypeSymbol>> parameterTypes,
            FunctionMethodSymbol parent) {
            if (parameterTypes.Length > 0) {
                var paramsBuilder = ArrayBuilder<FunctionParameterSymbol>.GetInstance(parameterTypes.Length);

                for (var i = 0; i < parameterTypes.Length; i++) {
                    var param = parameterTypes[i];
                    // var paramRefCustomMods = CSharpCustomModifier.Convert(param.RefCustomModifiers);
                    // var paramType = TypeWithAnnotations.Create(param.Type, customModifiers: CSharpCustomModifier.Convert(param.CustomModifiers));
                    var paramType = new TypeWithAnnotations(param.type);
                    // var paramRefKind = getRefKind(param, /*paramRefCustomMods, */RefKind.In, RefKind.Out, requiresLocationAllowed: true);
                    var paramRefKind = param.isByRef ? RefKind.Ref : RefKind.None;
                    paramsBuilder.Add(new FunctionParameterSymbol(paramType, paramRefKind, i, parent/*, paramRefCustomMods*/));
                }

                return paramsBuilder.ToImmutableAndFree();
            } else {
                return ImmutableArray<FunctionParameterSymbol>.Empty;
            }
        }
    }

    internal static FunctionMethodSymbol CreateFromMetadata(
        ModuleSymbol containingModule,
        ImmutableArray<ParamInfo<TypeSymbol>> retAndParamTypes) {
        return new FunctionMethodSymbol(retAndParamTypes);
    }

    private FunctionMethodSymbol(
        RefKind refKind,
        TypeWithAnnotations returnType,
        FunctionTypeSyntax syntax,
        Binder typeBinder,
        BelteDiagnosticQueue diagnostics) {
        this.refKind = refKind;
        returnTypeWithAnnotations = returnType;

        _parameters = syntax.parameterList.parameters.Count > 0
            ? ParameterHelpers.MakeFunctionParameters(
                typeBinder,
                this,
                syntax.parameterList.parameters,
                diagnostics)
            : [];

        if (returnType.type.IsPointerOrFunctionPointer() || _parameters.Any(p => p.type.IsPointerOrFunctionPointer()))
            diagnostics.Push(Error.FunctionCannotContainPointer(syntax.location));
    }

    private FunctionMethodSymbol(
        RefKind refKind,
        TypeWithAnnotations returnTypeWithAnnotations,
        ImmutableArray<TypeWithAnnotations> parameterTypes,
        ImmutableArray<RefKind> parameterRefKinds) {
        this.refKind = refKind;
        this.returnTypeWithAnnotations = returnTypeWithAnnotations;

        _parameters = parameterTypes.ZipAsArray(parameterRefKinds, this,
            (type, refKind, i, arg) => {
                return new FunctionParameterSymbol(type, refKind, i, arg);
            }
        );
    }

    internal static FunctionMethodSymbol CreateFromParts(
        TypeWithAnnotations returnTypeWithAnnotations,
        RefKind returnRefKind,
        ImmutableArray<TypeWithAnnotations> parameterTypes,
        ImmutableArray<RefKind> parameterRefKinds) {
        return new FunctionMethodSymbol(
            returnRefKind,
            returnTypeWithAnnotations,
            parameterTypes,
            parameterRefKinds
        );
    }

    internal static FunctionMethodSymbol CreateFromSource(
        FunctionTypeSyntax syntax,
        Binder typeBinder,
        BelteDiagnosticQueue diagnostics,
        ConsList<TypeSymbol> basesBeingResolved) {

        var refKind = RefKind.None;
        TypeWithAnnotations returnType;

        returnType = typeBinder.BindType(syntax.returnType, diagnostics, basesBeingResolved);

        // TODO Error checks
        // if (returnType.IsVoidType() && refKind != RefKind.None) {
        //     diagnostics.Add(ErrorCode.ERR_NoVoidHere, returnTypeParameter.Location);
        // } else if (returnType.IsStatic) {
        //     diagnostics.Add(ErrorFacts.GetStaticClassReturnCode(useWarning: false), returnTypeParameter.Location, returnType);
        // } else if (returnType.IsRestrictedType(ignoreSpanLikeTypes: true)) {
        //     diagnostics.Add(ErrorCode.ERR_MethodReturnCantBeRefAny, returnTypeParameter.Location, returnType);
        // }

        return new FunctionMethodSymbol(
            refKind,
            returnType,
            syntax,
            typeBinder,
            diagnostics
        );
    }

    internal FunctionMethodSymbol SubstituteParameterSymbols(
        TypeWithAnnotations substitutedReturnType,
        ImmutableArray<TypeOrConstant> substitutedParameterTypes) {
        return new FunctionMethodSymbol(
            refKind,
            substitutedReturnType,
            parameters,
            substitutedParameterTypes
        );
    }

    internal override bool Equals(Symbol other, TypeCompareKind compareKind) {
        if (!(other is FunctionMethodSymbol method)) {
            return false;
        }

        return Equals(method, compareKind);
    }

    internal bool Equals(FunctionMethodSymbol other, TypeCompareKind compareKind) {
        if (ReferenceEquals(this, other))
            return true;

        if (!EqualsNoParameters(other, compareKind))
            return false;

        return _parameters.SequenceEqual(other._parameters, compareKind,
             (param1, param2, compareKind) => param1.MethodEqualityChecks(param2, compareKind));
    }

    private bool EqualsNoParameters(FunctionMethodSymbol other, TypeCompareKind compareKind) {
        if (!FunctionTypeSymbol.RefKindEquals(compareKind, refKind, other.refKind)
            || !returnTypeWithAnnotations.Equals(other.returnTypeWithAnnotations, compareKind)) {
            return false;
        }

        // if ((compareKind & TypeCompareKind.IgnoreArraySizesAndLowerBounds) != 0) {
        //     if (CallingConvention.IsCallingConvention(CallingConvention.Unmanaged)
        //         && !GetCallingConventionModifiers().SetEqualsWithoutIntermediateHashSet(other.GetCallingConventionModifiers())) {
        //         return false;
        //     }
        // }

        return true;
    }

    public override int GetHashCode() {
        var currentHash = GetHashCodeNoParameters();

        foreach (var param in _parameters)
            currentHash = Hash.Combine(param.MethodHashCode(), currentHash);

        return currentHash;
    }

    internal FunctionMethodSymbol ApplyNullableTransforms(
        byte defaultTransformFlag,
        ImmutableArray<byte> transforms,
        ref int position) {
        var madeChanges = returnTypeWithAnnotations.ApplyNullableTransforms(
            defaultTransformFlag,
            transforms,
            ref position,
            out var newReturnType
        );

        var newParamTypes = ImmutableArray<TypeOrConstant>.Empty;

        if (!parameters.IsEmpty) {
            var paramTypesBuilder = ArrayBuilder<TypeOrConstant>.GetInstance(parameters.Length);
            var madeParamChanges = false;

            foreach (var param in parameters) {
                madeParamChanges |= param.typeWithAnnotations
                    .ApplyNullableTransforms(defaultTransformFlag, transforms, ref position, out var newParamType);
                paramTypesBuilder.Add(new TypeOrConstant(newParamType));
            }

            if (madeParamChanges) {
                newParamTypes = paramTypesBuilder.ToImmutableAndFree();
                madeChanges = true;
            } else {
                paramTypesBuilder.Free();
                newParamTypes = parameterTypesWithAnnotations.Select(p => new TypeOrConstant(p)).ToImmutableArray();
            }
        }

        if (madeChanges)
            return SubstituteParameterSymbols(newReturnType, newParamTypes);
        else
            return this;
    }

    internal int GetHashCodeNoParameters() {
        return Hash.Combine(returnType,
            Hash.Combine(((int)callingConvention).GetHashCode(),
                ((int)FunctionPointerTypeSymbol.GetRefKindForHashCode(refKind)).GetHashCode()));
    }

    public override bool returnsVoid => returnTypeWithAnnotations.IsVoidType();

    public override RefKind refKind { get; }

    internal override CallingConvention callingConvention => CallingConvention.Default;

    internal override TypeWithAnnotations returnTypeWithAnnotations { get; }

    internal override ImmutableArray<ParameterSymbol> parameters
        => _parameters.Cast<FunctionParameterSymbol, ParameterSymbol>();

    public override MethodKind methodKind => MethodKind.FunctionPointerSignature;

    internal override Symbol containingSymbol => null;

    public override int arity => 0;

    public override ImmutableArray<TemplateParameterSymbol> templateParameters => [];

    public override ImmutableArray<BoundExpression> templateConstraints => [];

    internal override bool hidesBaseMethodsByName => false;

    internal override ImmutableArray<TextLocation> locations => [];

    internal override TextLocation location => null;

    internal override ImmutableArray<SyntaxReference> declaringSyntaxReferences => [];

    internal override SyntaxReference syntaxReference => null;

    internal override Accessibility declaredAccessibility => Accessibility.NotApplicable;

    internal override bool isStatic => false;

    internal override bool isVirtual => false;

    internal override bool isOverride => false;

    internal override bool isAbstract => false;

    internal override bool isSealed => false;

    internal override bool isExtern => false;

    internal override bool isImplicitlyDeclared => true;

    internal override bool isDeclaredConst => false;

    internal override bool hasUnscopedRefAttribute => false;

    public override ImmutableArray<TypeOrConstant> templateArguments => [];

    internal override bool hasSpecialName => false;

    internal override DllImportData GetDllImportData() {
        throw ExceptionUtilities.Unreachable();
    }

    internal override int CalculateLocalSyntaxOffset(int localPosition, SyntaxTree localTree) {
        throw ExceptionUtilities.Unreachable();
    }

    internal override bool IsMetadataVirtual(bool forceComplete = false) {
        return false;
    }
}
